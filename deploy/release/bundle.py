#!/usr/bin/env python3
"""Read-only validation of an unpacked, trusted release download."""
from pathlib import Path
import argparse
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'self-host'))
from release_bundle import validate_bundle
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('command',choices=['verify']);parser.add_argument('directory',type=Path);args=parser.parse_args()
    validate_bundle(args.directory);print('Release file checksums and complete image inventory verified. Verify download provenance separately.')
