from __future__ import annotations

import ctypes

from ui import run_app


APP_ID = "NeuberJone.ListForge.1"


def main() -> None:
    try:
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(APP_ID)
    except Exception:
        pass

    run_app()


if __name__ == "__main__":
    main()