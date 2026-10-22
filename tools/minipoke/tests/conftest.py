import sys
from pathlib import Path

# The training scripts are plain modules in tools/minipoke, not a package.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
