#!/usr/bin/env python3
"""
Auto Setup Script

Installs all required dependencies for the DEFCON Translation Project.
Creates a virtual environment and installs all packages.
"""

import sys
import subprocess
import shutil
import os
import platform
import venv

# ANSI colors
GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
RESET = "\033[0m"
BOLD = "\033[1m"

VENV_NAME = "LaVillaHacker"

# WhisperX requires Python >=3.10, <3.14
WHISPERX_MAX_PYTHON = (3, 14)
WHISPERX_MIN_PYTHON = (3, 10)


def find_compatible_python() -> str:
    """Find a Python version compatible with WhisperX (<3.14, >=3.10)."""
    current_version = sys.version_info[:2]

    # If current Python is compatible, use it
    if WHISPERX_MIN_PYTHON <= current_version < WHISPERX_MAX_PYTHON:
        return sys.executable

    print(f"\n{YELLOW}Python {current_version[0]}.{current_version[1]} is not compatible with WhisperX{RESET}")
    print(f"WhisperX requires Python >=3.10, <3.14")
    print(f"\n{CYAN}Searching for compatible Python version...{RESET}")

    # Look for compatible Python versions (prefer newer)
    candidates = []

    # Check common Homebrew paths (macOS)
    brew_paths = [
        '/opt/homebrew/opt/python@3.13/bin/python3.13',
        '/opt/homebrew/opt/python@3.12/bin/python3.12',
        '/opt/homebrew/opt/python@3.11/bin/python3.11',
        '/opt/homebrew/opt/python@3.10/bin/python3.10',
        '/usr/local/opt/python@3.13/bin/python3.13',
        '/usr/local/opt/python@3.12/bin/python3.12',
        '/usr/local/opt/python@3.11/bin/python3.11',
        '/usr/local/opt/python@3.10/bin/python3.10',
    ]

    # Check system paths
    system_paths = [
        'python3.13', 'python3.12', 'python3.11', 'python3.10'
    ]

    for path in brew_paths:
        if os.path.exists(path):
            candidates.append(path)

    for cmd in system_paths:
        full_path = shutil.which(cmd)
        if full_path and full_path not in candidates:
            candidates.append(full_path)

    # Verify each candidate
    for python_path in candidates:
        try:
            result = subprocess.run(
                [python_path, '-c', 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")'],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0:
                version_str = result.stdout.strip()
                major, minor = map(int, version_str.split('.'))
                if WHISPERX_MIN_PYTHON <= (major, minor) < WHISPERX_MAX_PYTHON:
                    print(f"   {GREEN}[FOUND]{RESET} {python_path} (Python {version_str})")
                    return python_path
        except:
            continue

    # No compatible version found
    print(f"\n{RED}No compatible Python found!{RESET}")
    print(f"\n{BOLD}Install Python 3.13:{RESET}")
    if platform.system().lower() == 'darwin':
        print("  brew install python@3.13")
    else:
        print("  # Install Python 3.13 from https://python.org")
    print(f"\nThen run this script again with:")
    print(f"  /path/to/python3.13 {os.path.basename(__file__)}")

    return ""


def run_command(cmd: list, description: str, env=None) -> bool:
    """Run a command and return success status."""
    print(f"\n{CYAN}>> {description}{RESET}")
    print(f"   Running: {' '.join(cmd)}")

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=600,  # 10 min timeout for large packages
            env=env
        )

        if result.returncode == 0:
            print(f"   {GREEN}[SUCCESS]{RESET}")
            return True
        else:
            print(f"   {RED}[FAILED]{RESET}")
            if result.stderr:
                # Show first 300 chars of error
                print(f"   Error: {result.stderr[:300]}")
            return False

    except subprocess.TimeoutExpired:
        print(f"   {RED}[TIMEOUT]{RESET}")
        return False
    except Exception as e:
        print(f"   {RED}[ERROR]{RESET} {e}")
        return False


def check_python_version() -> bool:
    """Verify Python version >= 3.9"""
    version = sys.version_info
    if version >= (3, 9):
        print(f"{GREEN}[OK]{RESET} Python {version.major}.{version.minor}.{version.micro}")
        return True
    else:
        print(f"{RED}[FAIL]{RESET} Python 3.9+ required, found {version.major}.{version.minor}")
        return False


def create_virtual_environment(venv_path: str, python_path: str = None) -> bool:
    """Create a virtual environment using specified Python."""
    if os.path.exists(venv_path):
        print(f"\n{GREEN}[OK]{RESET} Virtual environment '{VENV_NAME}' already exists")
        return True

    python_cmd = python_path or sys.executable
    print(f"\n{CYAN}>> Creating virtual environment '{VENV_NAME}'...{RESET}")
    print(f"   Using: {python_cmd}")

    try:
        result = subprocess.run(
            [python_cmd, '-m', 'venv', venv_path],
            capture_output=True,
            text=True,
            timeout=60
        )
        if result.returncode == 0:
            print(f"   {GREEN}[SUCCESS]{RESET} Created {venv_path}")
            return True
        else:
            print(f"   {RED}[FAILED]{RESET} {result.stderr}")
            return False
    except Exception as e:
        print(f"   {RED}[FAILED]{RESET} {e}")
        return False


def get_venv_paths(venv_path: str) -> tuple:
    """Get pip and python paths inside venv."""
    if platform.system() == 'Windows':
        pip_path = os.path.join(venv_path, 'Scripts', 'pip')
        python_path = os.path.join(venv_path, 'Scripts', 'python')
    else:
        pip_path = os.path.join(venv_path, 'bin', 'pip')
        python_path = os.path.join(venv_path, 'bin', 'python')

    return pip_path, python_path


def install_basic_packages(pip_path: str) -> bool:
    """Install basic pip packages."""
    packages = [
        'requests',
        'ffmpeg-python',
        'openai',
        'psycopg2-binary',
    ]

    cmd = [pip_path, 'install', '--upgrade'] + packages
    return run_command(cmd, "Installing basic packages (requests, ffmpeg-python, openai, psycopg2)")


def install_pytorch(pip_path: str) -> bool:
    """Install PyTorch with CUDA support if available."""
    system = platform.system().lower()

    # Check for NVIDIA GPU
    has_nvidia = False
    if shutil.which('nvidia-smi'):
        try:
            result = subprocess.run(['nvidia-smi'], capture_output=True, timeout=10)
            has_nvidia = result.returncode == 0
        except:
            pass

    if has_nvidia:
        print(f"\n{GREEN}NVIDIA GPU detected - installing PyTorch with CUDA{RESET}")
        cmd = [
            pip_path, 'install', 'torch', 'torchvision', 'torchaudio',
            '--index-url', 'https://download.pytorch.org/whl/cu121'
        ]
    elif system == 'darwin':
        print(f"\n{CYAN}macOS detected - installing PyTorch with MPS support{RESET}")
        cmd = [pip_path, 'install', 'torch', 'torchvision', 'torchaudio']
    else:
        print(f"\n{YELLOW}No GPU detected - installing CPU-only PyTorch{RESET}")
        cmd = [
            pip_path, 'install', 'torch', 'torchvision', 'torchaudio',
            '--index-url', 'https://download.pytorch.org/whl/cpu'
        ]

    return run_command(cmd, "Installing PyTorch")


def install_whisperx(pip_path: str) -> bool:
    """Install WhisperX from GitHub."""
    cmd = [pip_path, 'install', 'git+https://github.com/m-bain/whisperx.git']
    return run_command(cmd, "Installing WhisperX (this may take a while)")


def setup_config_file() -> bool:
    """Create config.json from example if it doesn't exist."""
    config_file = 'config.json'
    example_file = 'config.json.example'

    if os.path.exists(config_file):
        print(f"\n{GREEN}[OK]{RESET} config.json already exists")
        return True

    if os.path.exists(example_file):
        try:
            shutil.copy(example_file, config_file)
            print(f"\n{GREEN}[OK]{RESET} Created config.json from example")
            print(f"   {YELLOW}Remember to edit config.json and set your API key!{RESET}")
            return True
        except Exception as e:
            print(f"\n{RED}[FAIL]{RESET} Could not create config.json: {e}")
            return False
    else:
        print(f"\n{YELLOW}[SKIP]{RESET} config.json.example not found")
        return False


def check_ffmpeg() -> bool:
    """Check if FFmpeg is installed."""
    if shutil.which('ffmpeg'):
        print(f"\n{GREEN}[OK]{RESET} FFmpeg is installed")
        return True
    else:
        print(f"\n{RED}[MISSING]{RESET} FFmpeg not found")
        return False


def check_ollama() -> bool:
    """Check if Ollama is installed."""
    if shutil.which('ollama'):
        print(f"\n{GREEN}[OK]{RESET} Ollama is installed")
        return True
    else:
        print(f"\n{RED}[MISSING]{RESET} Ollama not found")
        return False


def install_brew_package(package: str) -> bool:
    """Install a package via Homebrew (macOS only)."""
    if platform.system().lower() != 'darwin':
        print(f"   {YELLOW}[SKIP]{RESET} Homebrew only available on macOS")
        return False

    if not shutil.which('brew'):
        print(f"   {RED}[FAIL]{RESET} Homebrew not installed")
        print(f"   Install from: https://brew.sh")
        return False

    return run_command(['brew', 'install', package], f"Installing {package} via Homebrew")


def install_system_tools() -> tuple:
    """Install FFmpeg and Ollama if missing."""
    system = platform.system().lower()

    ffmpeg_ok = check_ffmpeg()
    ollama_ok = check_ollama()

    if system == 'darwin':
        # macOS - use Homebrew
        if not ffmpeg_ok:
            print(f"\n{CYAN}Attempting to install FFmpeg...{RESET}")
            ffmpeg_ok = install_brew_package('ffmpeg')
            if ffmpeg_ok:
                ffmpeg_ok = check_ffmpeg()  # Verify

        if not ollama_ok:
            print(f"\n{CYAN}Attempting to install Ollama...{RESET}")
            ollama_ok = install_brew_package('ollama')
            if ollama_ok:
                ollama_ok = check_ollama()  # Verify

    return ffmpeg_ok, ollama_ok


def pull_ollama_model() -> bool:
    """Pull the default Ollama model for translation."""
    if not shutil.which('ollama'):
        return False

    model = "qwen2.5:7b"

    # Check if model already exists
    try:
        result = subprocess.run(
            ['ollama', 'list'],
            capture_output=True,
            text=True,
            timeout=10
        )
        if model.split(':')[0] in result.stdout:
            print(f"\n{GREEN}[OK]{RESET} Ollama model '{model}' already available")
            return True
    except:
        pass

    print(f"\n{CYAN}Pulling Ollama model '{model}' (this may take a while)...{RESET}")
    return run_command(['ollama', 'pull', model], f"Pulling {model}")


def print_system_install_instructions():
    """Print instructions for installing system tools."""
    system = platform.system().lower()

    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}System Tools Installation{RESET}")
    print(f"{'='*60}")

    # FFmpeg
    if not shutil.which('ffmpeg'):
        print(f"\n{BOLD}FFmpeg:{RESET}")
        if system == 'darwin':
            print("  brew install ffmpeg")
        elif system == 'linux':
            print("  sudo apt install ffmpeg      # Debian/Ubuntu")
            print("  sudo dnf install ffmpeg      # Fedora")
            print("  sudo pacman -S ffmpeg        # Arch")
        elif system == 'windows':
            print("  choco install ffmpeg         # Chocolatey")
            print("  winget install ffmpeg        # WinGet")
            print("  Or download from: https://ffmpeg.org/download.html")

    # Ollama
    if not shutil.which('ollama'):
        print(f"\n{BOLD}Ollama (for local AI translation):{RESET}")
        if system == 'darwin':
            print("  brew install ollama")
            print("  Or download from: https://ollama.ai")
        elif system == 'linux':
            print("  curl -fsSL https://ollama.ai/install.sh | sh")
        elif system == 'windows':
            print("  Download from: https://ollama.ai")

        print(f"\n  After installing Ollama, pull the translation model:")
        print(f"  ollama pull qwen2.5:7b")


def create_directories():
    """Create required directories."""
    dirs = ['downloads', 'audio', 'vtt']

    print(f"\n{CYAN}Creating directories...{RESET}")
    for d in dirs:
        os.makedirs(d, exist_ok=True)
        print(f"   {GREEN}[OK]{RESET} {d}/")


def create_activation_script(venv_path: str):
    """Create a helper script to activate venv and run commands."""
    system = platform.system().lower()

    if system == 'windows':
        script_content = f'''@echo off
call {venv_path}\\Scripts\\activate.bat
python %*
'''
        script_name = 'run.bat'
    else:
        script_content = f'''#!/bin/bash
source {venv_path}/bin/activate
python3 "$@"
'''
        script_name = 'run.sh'

    with open(script_name, 'w') as f:
        f.write(script_content)

    if system != 'windows':
        os.chmod(script_name, 0o755)

    print(f"\n{GREEN}[OK]{RESET} Created {script_name} helper script")


def main():
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}  DEFCON Translation Project - Auto Setup{RESET}")
    print(f"{BOLD}  Virtual Environment: {VENV_NAME}{RESET}")
    print(f"{'='*60}\n")

    # Check Python version
    print(f"{BOLD}Checking Python version...{RESET}")
    if not check_python_version():
        print(f"\n{RED}Setup aborted. Please upgrade Python to 3.9+{RESET}")
        return 1

    # Get script directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    os.chdir(script_dir)
    venv_path = os.path.join(script_dir, VENV_NAME)

    # Find compatible Python for WhisperX
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Python Compatibility Check{RESET}")
    print(f"{'='*60}")

    compatible_python = find_compatible_python()
    if not compatible_python:
        return 1

    # Create virtual environment
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Virtual Environment Setup{RESET}")
    print(f"{'='*60}")

    if not create_virtual_environment(venv_path, compatible_python):
        print(f"\n{RED}Failed to create virtual environment{RESET}")
        return 1

    # Get venv pip path
    pip_path, python_path = get_venv_paths(venv_path)

    # Upgrade pip in venv
    run_command([pip_path, 'install', '--upgrade', 'pip'], "Upgrading pip in venv")

    # Track results
    results = []

    # Install packages
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Installing Python Packages (in {VENV_NAME}){RESET}")
    print(f"{'='*60}")

    results.append(("Basic packages", install_basic_packages(pip_path)))
    results.append(("PyTorch", install_pytorch(pip_path)))
    results.append(("WhisperX", install_whisperx(pip_path)))

    # Setup config
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Configuration{RESET}")
    print(f"{'='*60}")

    results.append(("Config file", setup_config_file()))

    # Create directories
    create_directories()

    # Create helper script
    create_activation_script(venv_path)

    # Install system tools
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}System Tools (FFmpeg, Ollama){RESET}")
    print(f"{'='*60}")

    ffmpeg_ok, ollama_ok = install_system_tools()

    # Pull Ollama model if Ollama is installed
    if ollama_ok:
        pull_ollama_model()

    # Print manual install instructions if still missing
    if not ffmpeg_ok or not ollama_ok:
        print_system_install_instructions()

    # Summary
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Setup Summary{RESET}")
    print(f"{'='*60}\n")

    all_ok = True
    for name, status in results:
        icon = f"{GREEN}[OK]{RESET}" if status else f"{RED}[FAIL]{RESET}"
        print(f"  {icon} {name}")
        if not status:
            all_ok = False

    # System tools
    icon = f"{GREEN}[OK]{RESET}" if ffmpeg_ok else f"{YELLOW}[MANUAL]{RESET}"
    print(f"  {icon} FFmpeg")

    icon = f"{GREEN}[OK]{RESET}" if ollama_ok else f"{YELLOW}[MANUAL]{RESET}"
    print(f"  {icon} Ollama")

    # Usage instructions
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}How to Use{RESET}")
    print(f"{'='*60}")

    system = platform.system().lower()
    if system == 'windows':
        activate_cmd = f"{VENV_NAME}\\Scripts\\activate"
        run_cmd = "run.bat"
    else:
        activate_cmd = f"source {VENV_NAME}/bin/activate"
        run_cmd = "./run.sh"

    print(f"\n{CYAN}Option 1: Activate venv manually{RESET}")
    print(f"  {activate_cmd}")
    print(f"  python3 09_Auto_Pipeline.py <input_file>")

    print(f"\n{CYAN}Option 2: Use helper script{RESET}")
    print(f"  {run_cmd} 09_Auto_Pipeline.py <input_file>")
    print(f"  {run_cmd} 10_DoctorConfigValidator.py")

    if all_ok and ffmpeg_ok and ollama_ok:
        print(f"\n{GREEN}{BOLD}Setup complete! All dependencies installed.{RESET}")
        return 0
    else:
        print(f"\n{YELLOW}Setup partially complete. Install system tools above.{RESET}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
