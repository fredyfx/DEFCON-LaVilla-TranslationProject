#!/usr/bin/env python3
"""
Auto Setup Script

Installs all required dependencies for the DEFCON Translation Project.
Creates a virtual environment and installs all packages.

Supports: macOS, Linux (Debian/Ubuntu, Fedora, Arch), Windows
"""

import sys
import subprocess
import shutil
import os
import platform
import venv
from dataclasses import dataclass
from typing import Optional, List

# ANSI colors (disabled on Windows unless using Windows Terminal)
if platform.system() == 'Windows':
    # Enable ANSI on Windows 10+
    try:
        import ctypes
        kernel32 = ctypes.windll.kernel32
        kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)
        GREEN = "\033[92m"
        RED = "\033[91m"
        YELLOW = "\033[93m"
        CYAN = "\033[96m"
        RESET = "\033[0m"
        BOLD = "\033[1m"
    except:
        GREEN = RED = YELLOW = CYAN = RESET = BOLD = ""
else:
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


@dataclass
class OSInfo:
    """Operating system information."""
    name: str  # 'macos', 'linux', 'windows'
    distro: str  # 'debian', 'fedora', 'arch', 'macos', 'windows'
    package_manager: str  # 'brew', 'apt', 'dnf', 'pacman', 'winget', 'choco', ''
    pm_install_cmd: List[str]  # ['brew', 'install'] or ['sudo', 'apt', 'install', '-y']


def detect_os() -> OSInfo:
    """Detect OS and available package manager."""
    system = platform.system().lower()

    if system == 'darwin':
        # macOS
        pm = 'brew' if shutil.which('brew') else ''
        return OSInfo(
            name='macos',
            distro='macos',
            package_manager=pm,
            pm_install_cmd=['brew', 'install'] if pm else []
        )

    elif system == 'linux':
        # Detect Linux distro
        distro = 'unknown'
        try:
            with open('/etc/os-release', 'r') as f:
                content = f.read().lower()
                if 'ubuntu' in content or 'debian' in content or 'mint' in content:
                    distro = 'debian'
                elif 'fedora' in content or 'rhel' in content or 'centos' in content:
                    distro = 'fedora'
                elif 'arch' in content or 'manjaro' in content:
                    distro = 'arch'
                elif 'suse' in content:
                    distro = 'suse'
        except:
            pass

        # Detect package manager
        if shutil.which('apt'):
            return OSInfo('linux', distro or 'debian', 'apt', ['sudo', 'apt', 'install', '-y'])
        elif shutil.which('dnf'):
            return OSInfo('linux', distro or 'fedora', 'dnf', ['sudo', 'dnf', 'install', '-y'])
        elif shutil.which('pacman'):
            return OSInfo('linux', distro or 'arch', 'pacman', ['sudo', 'pacman', '-S', '--noconfirm'])
        elif shutil.which('zypper'):
            return OSInfo('linux', distro or 'suse', 'zypper', ['sudo', 'zypper', 'install', '-y'])
        elif shutil.which('brew'):
            return OSInfo('linux', distro, 'brew', ['brew', 'install'])
        else:
            return OSInfo('linux', distro, '', [])

    elif system == 'windows':
        # Windows - check for winget or choco
        if shutil.which('winget'):
            return OSInfo('windows', 'windows', 'winget', ['winget', 'install', '--silent'])
        elif shutil.which('choco'):
            return OSInfo('windows', 'windows', 'choco', ['choco', 'install', '-y'])
        else:
            return OSInfo('windows', 'windows', '', [])

    return OSInfo('unknown', 'unknown', '', [])


def print_os_info(os_info: OSInfo):
    """Print detected OS information."""
    print(f"\n{BOLD}Detected System:{RESET}")
    print(f"  OS: {os_info.name}")
    if os_info.name == 'linux':
        print(f"  Distro: {os_info.distro}")
    if os_info.package_manager:
        print(f"  Package Manager: {GREEN}{os_info.package_manager}{RESET}")
    else:
        print(f"  Package Manager: {RED}Not found{RESET}")


def find_compatible_python(os_info: OSInfo) -> str:
    """Find a Python version compatible with WhisperX (<3.14, >=3.10)."""
    current_version = sys.version_info[:2]

    # If current Python is compatible, use it
    if WHISPERX_MIN_PYTHON <= current_version < WHISPERX_MAX_PYTHON:
        return sys.executable

    print(f"\n{YELLOW}Python {current_version[0]}.{current_version[1]} is not compatible with WhisperX{RESET}")
    print(f"WhisperX requires Python >=3.10, <3.14")
    print(f"\n{CYAN}Searching for compatible Python version...{RESET}")

    candidates = []

    if os_info.name == 'macos':
        # Homebrew paths (macOS)
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
        for path in brew_paths:
            if os.path.exists(path):
                candidates.append(path)

    elif os_info.name == 'windows':
        # Windows paths
        win_paths = [
            os.path.expandvars(r'%LOCALAPPDATA%\Programs\Python\Python313\python.exe'),
            os.path.expandvars(r'%LOCALAPPDATA%\Programs\Python\Python312\python.exe'),
            os.path.expandvars(r'%LOCALAPPDATA%\Programs\Python\Python311\python.exe'),
            os.path.expandvars(r'%LOCALAPPDATA%\Programs\Python\Python310\python.exe'),
            r'C:\Python313\python.exe',
            r'C:\Python312\python.exe',
            r'C:\Python311\python.exe',
            r'C:\Python310\python.exe',
        ]
        for path in win_paths:
            if os.path.exists(path):
                candidates.append(path)

    # Check system PATH (all platforms)
    if os_info.name == 'windows':
        system_cmds = ['python3.13', 'python3.12', 'python3.11', 'python3.10', 'python']
    else:
        system_cmds = ['python3.13', 'python3.12', 'python3.11', 'python3.10']

    for cmd in system_cmds:
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

    # No compatible version found - print install instructions
    print(f"\n{RED}No compatible Python found!{RESET}")
    print(f"\n{BOLD}Install Python 3.13:{RESET}")

    if os_info.name == 'macos':
        print("  brew install python@3.13")
    elif os_info.name == 'linux':
        if os_info.distro == 'debian':
            print("  sudo apt update && sudo apt install python3.13 python3.13-venv")
        elif os_info.distro == 'fedora':
            print("  sudo dnf install python3.13")
        elif os_info.distro == 'arch':
            print("  sudo pacman -S python")
        else:
            print("  # Install Python 3.13 from https://python.org")
    elif os_info.name == 'windows':
        print("  winget install Python.Python.3.13")
        print("  # Or download from https://python.org")

    print(f"\nThen run this script again.")
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
        'markitdown',
    ]

    cmd = [pip_path, 'install', '--upgrade'] + packages
    return run_command(cmd, "Installing basic packages (requests, ffmpeg-python, openai, psycopg2, markitdown)")


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


def install_system_package(os_info: OSInfo, package: str, alt_names: dict = None) -> bool:
    """Install a package using the system package manager.

    Args:
        os_info: OS information
        package: Default package name
        alt_names: Dict of {package_manager: package_name} for alternatives
    """
    if not os_info.package_manager:
        print(f"   {YELLOW}[SKIP]{RESET} No package manager found")
        return False

    # Get package name for this package manager
    pkg_name = package
    if alt_names and os_info.package_manager in alt_names:
        pkg_name = alt_names[os_info.package_manager]

    cmd = os_info.pm_install_cmd + [pkg_name]
    return run_command(cmd, f"Installing {pkg_name} via {os_info.package_manager}")


def install_system_tools(os_info: OSInfo) -> tuple:
    """Install FFmpeg and Ollama if missing."""
    ffmpeg_ok = check_ffmpeg()
    ollama_ok = check_ollama()

    if not os_info.package_manager:
        print(f"\n{YELLOW}No package manager detected. Install manually:{RESET}")
        if not ffmpeg_ok:
            print(f"  - FFmpeg: https://ffmpeg.org/download.html")
        if not ollama_ok:
            print(f"  - Ollama: https://ollama.ai")
        return ffmpeg_ok, ollama_ok

    # FFmpeg package names vary by distro
    ffmpeg_names = {
        'apt': 'ffmpeg',
        'dnf': 'ffmpeg-free',  # Fedora uses ffmpeg-free in default repos
        'pacman': 'ffmpeg',
        'brew': 'ffmpeg',
        'winget': 'Gyan.FFmpeg',
        'choco': 'ffmpeg',
    }

    # Ollama package names
    ollama_names = {
        'brew': 'ollama',
        'winget': 'Ollama.Ollama',
        'choco': 'ollama',
        # Linux distros typically need curl install
    }

    if not ffmpeg_ok:
        print(f"\n{CYAN}Attempting to install FFmpeg...{RESET}")
        if install_system_package(os_info, 'ffmpeg', ffmpeg_names):
            ffmpeg_ok = check_ffmpeg()

    if not ollama_ok:
        print(f"\n{CYAN}Attempting to install Ollama...{RESET}")

        # Special handling for Linux (curl script)
        if os_info.name == 'linux' and os_info.package_manager not in ['brew']:
            print(f"   {CYAN}Installing Ollama via official script...{RESET}")
            result = run_command(
                ['bash', '-c', 'curl -fsSL https://ollama.ai/install.sh | sh'],
                "Installing Ollama"
            )
            if result:
                ollama_ok = check_ollama()
        elif os_info.package_manager in ollama_names:
            if install_system_package(os_info, 'ollama', ollama_names):
                ollama_ok = check_ollama()
        else:
            print(f"   {YELLOW}[MANUAL]{RESET} Install Ollama from https://ollama.ai")

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


def print_system_install_instructions(os_info: OSInfo):
    """Print instructions for installing system tools."""
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Manual Installation Required{RESET}")
    print(f"{'='*60}")

    # FFmpeg
    if not shutil.which('ffmpeg'):
        print(f"\n{BOLD}FFmpeg:{RESET}")
        if os_info.name == 'macos':
            print("  brew install ffmpeg")
        elif os_info.name == 'linux':
            if os_info.distro == 'debian':
                print("  sudo apt install ffmpeg")
            elif os_info.distro == 'fedora':
                print("  sudo dnf install ffmpeg-free")
            elif os_info.distro == 'arch':
                print("  sudo pacman -S ffmpeg")
            else:
                print("  # Install ffmpeg using your package manager")
        elif os_info.name == 'windows':
            print("  winget install Gyan.FFmpeg")
            print("  # Or: choco install ffmpeg")
            print("  # Or download from: https://ffmpeg.org/download.html")

    # Ollama
    if not shutil.which('ollama'):
        print(f"\n{BOLD}Ollama (for local AI translation):{RESET}")
        if os_info.name == 'macos':
            print("  brew install ollama")
        elif os_info.name == 'linux':
            print("  curl -fsSL https://ollama.ai/install.sh | sh")
        elif os_info.name == 'windows':
            print("  winget install Ollama.Ollama")
            print("  # Or download from: https://ollama.ai")

        print(f"\n  After installing Ollama, pull the translation model:")
        print(f"  ollama pull qwen2.5:7b")


def create_directories():
    """Create required directories."""
    dirs = ['downloads', 'audio', 'vtt', 'pdf_extracts']

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

    # Detect OS
    os_info = detect_os()
    print_os_info(os_info)

    # Check Python version
    print(f"\n{BOLD}Checking Python version...{RESET}")
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

    compatible_python = find_compatible_python(os_info)
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

    ffmpeg_ok, ollama_ok = install_system_tools(os_info)

    # Pull Ollama model if Ollama is installed
    if ollama_ok:
        pull_ollama_model()

    # Print manual install instructions if still missing
    if not ffmpeg_ok or not ollama_ok:
        print_system_install_instructions(os_info)

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
