#!/usr/bin/env python3
"""Stage a verified release outside Git so CLI deployment has no source Git metadata."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
import prepare_vercel

def stage(source, destination):
    source = source.resolve(); destination = destination.resolve()
    prepare_vercel.validate(source)
    destination.mkdir(parents=True, exist_ok=True)
    git = subprocess.run(['git', '-C', str(destination), 'rev-parse', '--show-toplevel'], capture_output=True)
    if git.returncode == 0: raise ValueError('Deployment staging must be outside every Git repository.')
    if destination == source or source.is_relative_to(destination): raise ValueError('Use a separate staging directory.')
    if (destination / 'public').exists(): prepare_vercel.validate(destination)
    with tempfile.TemporaryDirectory(prefix='.stage-', dir=destination) as temporary:
        temporary = Path(temporary)
        shutil.copytree(source / 'public', temporary / 'public')
        for name in ['vercel.json', 'release-manifest.json', '.vercelignore', '.gitignore']:
            shutil.copy2(source / name, temporary / name)
        prepare_vercel.validate(temporary)
        if (destination / 'public').exists(): shutil.rmtree(destination / 'public')
        (temporary / 'public').rename(destination / 'public')
        for name in ['vercel.json', 'release-manifest.json', '.vercelignore', '.gitignore']:
            shutil.copy2(temporary / name, destination / name)
    return prepare_vercel.validate(destination)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parents[1] / 'Deploy/Vercel')
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    try:
        release = stage(args.source, args.destination)
        print('Staged verified runtime files outside Git. Release: ' + release['release'])
        print('CLI directory: ' + str(args.destination.resolve()))
    except (OSError, ValueError) as error: parser.exit(1, 'Staging refused: ' + str(error) + '\n')

if __name__ == '__main__': main()
