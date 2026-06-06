#!/usr/bin/env python3
"""
Doctor Config Validator

Checks all required dependencies for the DEFCON Translation Project.
Run this before using any pipeline scripts to verify your environment.
"""

import sys
import subprocess
import shutil
from dataclasses import dataclass
from typing import Optional, Tuple

# ANSI colors
GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
RESET = "\033[0m"
BOLD = "\033[1m"


@dataclass
class CheckResult:
    name: str
    status: bool
    version: Optional[str] = None
    message: Optional[str] = None
    required_by: Optional[str] = None


def check_python_version() -> CheckResult:
    """Check Python version >= 3.9"""
    version = sys.version_info
    version_str = f"{version.major}.{version.minor}.{version.micro}"
    ok = version >= (3, 9)
    return CheckResult(
        name="Python",
        status=ok,
        version=version_str,
        message=None if ok else "Python 3.9+ required",
        required_by="All scripts"
    )


def check_package(package_name: str, import_name: str = None, required_by: str = "") -> CheckResult:
    """Check if a Python package is installed."""
    import_name = import_name or package_name
    try:
        module = __import__(import_name)
        version = getattr(module, '__version__', 'unknown')
        return CheckResult(
            name=package_name,
            status=True,
            version=version,
            required_by=required_by
        )
    except ImportError as e:
        return CheckResult(
            name=package_name,
            status=False,
            message=f"pip install {package_name}",
            required_by=required_by
        )


def check_ffmpeg_python() -> CheckResult:
    """Check ffmpeg-python package."""
    try:
        import ffmpeg
        return CheckResult(
            name="ffmpeg-python",
            status=True,
            version="installed",
            required_by="02_Extract_Audio, 09_Auto_Pipeline"
        )
    except ImportError:
        return CheckResult(
            name="ffmpeg-python",
            status=False,
            message="pip install ffmpeg-python",
            required_by="02_Extract_Audio, 09_Auto_Pipeline"
        )


def check_whisperx() -> CheckResult:
    """Check whisperx package."""
    try:
        import whisperx
        version = getattr(whisperx, '__version__', 'installed')
        return CheckResult(
            name="whisperx",
            status=True,
            version=version,
            required_by="03_Generate_VTT, 09_Auto_Pipeline"
        )
    except ImportError:
        return CheckResult(
            name="whisperx",
            status=False,
            message="pip install git+https://github.com/m-bain/whisperx.git",
            required_by="03_Generate_VTT, 09_Auto_Pipeline"
        )


def check_torch() -> CheckResult:
    """Check PyTorch installation."""
    try:
        import torch
        cuda_available = torch.cuda.is_available()
        version = torch.__version__
        if cuda_available:
            cuda_version = torch.version.cuda
            return CheckResult(
                name="PyTorch + CUDA",
                status=True,
                version=f"{version} (CUDA {cuda_version})",
                required_by="whisperx"
            )
        else:
            return CheckResult(
                name="PyTorch",
                status=True,
                version=f"{version} (CPU only - GPU recommended)",
                message="CUDA not available, will be slow",
                required_by="whisperx"
            )
    except ImportError:
        return CheckResult(
            name="PyTorch",
            status=False,
            message="pip install torch torchvision torchaudio",
            required_by="whisperx"
        )


def check_system_command(cmd: str, name: str, required_by: str) -> CheckResult:
    """Check if a system command is available."""
    path = shutil.which(cmd)
    if path:
        # Try to get version
        try:
            result = subprocess.run(
                [cmd, '--version'],
                capture_output=True,
                text=True,
                timeout=5
            )
            version_line = result.stdout.strip().split('\n')[0] if result.stdout else "installed"
        except:
            version_line = "installed"

        return CheckResult(
            name=name,
            status=True,
            version=version_line[:50],
            required_by=required_by
        )
    else:
        return CheckResult(
            name=name,
            status=False,
            message=f"{cmd} not found in PATH",
            required_by=required_by
        )


def check_ollama() -> CheckResult:
    """Check Ollama installation and if it's running."""
    path = shutil.which('ollama')
    if not path:
        return CheckResult(
            name="Ollama",
            status=False,
            message="Install from https://ollama.ai",
            required_by="09_Auto_Pipeline (translation)"
        )

    # Check if running
    try:
        import requests
        response = requests.get('http://localhost:11434/api/tags', timeout=2)
        if response.status_code == 200:
            models = response.json().get('models', [])
            model_count = len(models)
            return CheckResult(
                name="Ollama",
                status=True,
                version=f"running ({model_count} models)",
                required_by="09_Auto_Pipeline (translation)"
            )
    except:
        pass

    return CheckResult(
        name="Ollama",
        status=True,
        version="installed (not running)",
        message="Start with: ollama serve",
        required_by="09_Auto_Pipeline (translation)"
    )


def check_config_file() -> CheckResult:
    """Check if config.json exists."""
    import os
    if os.path.exists('config.json'):
        try:
            import json
            with open('config.json', 'r') as f:
                config = json.load(f)
            api_key = config.get('api_key', '')
            if api_key == 'your-api-key-here':
                return CheckResult(
                    name="config.json",
                    status=False,
                    message="API key not configured",
                    required_by="09_Auto_Pipeline"
                )
            return CheckResult(
                name="config.json",
                status=True,
                version="configured",
                required_by="09_Auto_Pipeline"
            )
        except Exception as e:
            return CheckResult(
                name="config.json",
                status=False,
                message=f"Invalid JSON: {e}",
                required_by="09_Auto_Pipeline"
            )
    else:
        return CheckResult(
            name="config.json",
            status=False,
            message="Copy config.json.example to config.json",
            required_by="09_Auto_Pipeline"
        )


def print_result(result: CheckResult):
    """Print a single check result."""
    if result.status:
        icon = f"{GREEN}[OK]{RESET}"
        version_str = f" ({result.version})" if result.version else ""
        print(f"  {icon} {result.name}{version_str}")
        if result.message:
            print(f"       {YELLOW}Note: {result.message}{RESET}")
    else:
        icon = f"{RED}[FAIL]{RESET}"
        print(f"  {icon} {result.name}")
        if result.message:
            print(f"       {YELLOW}Fix: {result.message}{RESET}")
    if result.required_by:
        print(f"       {CYAN}Used by: {result.required_by}{RESET}")


def main():
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}  DEFCON Translation Project - Environment Check{RESET}")
    print(f"{BOLD}{'='*60}{RESET}\n")

    all_results = []

    # Python version
    print(f"{BOLD}Python Environment:{RESET}")
    result = check_python_version()
    print_result(result)
    all_results.append(result)
    print()

    # Core packages
    print(f"{BOLD}Core Python Packages:{RESET}")

    packages = [
        ("requests", "requests", "09_Auto_Pipeline"),
        ("ffmpeg-python", None, "02_Extract_Audio, 09_Auto_Pipeline"),
        ("openai", "openai", "06_Translation, 07_Translation_Portuguese"),
        ("psycopg2", "psycopg2", "08_DEFCON_Scanner"),
    ]

    for pkg in packages:
        if pkg[0] == "ffmpeg-python":
            result = check_ffmpeg_python()
        else:
            result = check_package(pkg[0], pkg[1], pkg[2])
        print_result(result)
        all_results.append(result)
    print()

    # ML packages
    print(f"{BOLD}Machine Learning Packages:{RESET}")

    result = check_torch()
    print_result(result)
    all_results.append(result)

    result = check_whisperx()
    print_result(result)
    all_results.append(result)
    print()

    # System tools
    print(f"{BOLD}System Tools:{RESET}")

    result = check_system_command("ffmpeg", "FFmpeg", "Audio extraction")
    print_result(result)
    all_results.append(result)

    result = check_ollama()
    print_result(result)
    all_results.append(result)
    print()

    # Config
    print(f"{BOLD}Configuration:{RESET}")
    result = check_config_file()
    print_result(result)
    all_results.append(result)
    print()

    # Summary
    passed = sum(1 for r in all_results if r.status)
    failed = sum(1 for r in all_results if not r.status)

    print(f"{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}Summary:{RESET} {GREEN}{passed} passed{RESET}, {RED}{failed} failed{RESET}")

    if failed == 0:
        print(f"\n{GREEN}{BOLD}All checks passed! Environment is ready.{RESET}\n")
        return 0
    else:
        print(f"\n{YELLOW}Fix the failed checks above before running the pipeline.{RESET}")
        print(f"\n{BOLD}Quick install commands:{RESET}")
        print("  pip install requests ffmpeg-python openai psycopg2-binary")
        print("  pip install torch torchvision torchaudio")
        print("  pip install git+https://github.com/m-bain/whisperx.git")
        print()
        return 1


if __name__ == "__main__":
    sys.exit(main())
