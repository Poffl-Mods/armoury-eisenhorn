"""Compile the API probe without installing or running a mod.
Usage: python3 compile_probe.py /path/to/Managed /path/to/MonoBleedingEdge
"""
from pathlib import Path
import subprocess
import sys
import tempfile

managed, mono = map(Path, sys.argv[1:3])
refs = [
    'Code', 'RogueTrader.GameCore', 'UnityEngine.CoreModule',
    'Owlcat.Runtime.Core', 'Newtonsoft.Json',
    'Kingmaker.PubSubSystem.Core.Interfaces', 'StateHasher',
    'Kingmaker.UI.Models.Tooltip.Base', 'Kingmaker.Blueprints.Base',
    'Kingmaker.Blueprints.JsonSystem.Hepers', 'Kingmaker.ElementsSystem.Interfaces',
]
with tempfile.TemporaryDirectory(prefix='eisenhorn-api-') as output:
    # Short paths avoid mcs interpreting the comma in the game's directory as a reference separator.
    for name in refs:
        Path(output, name + '.dll').symlink_to((managed / (name + '.dll')).resolve())
    result = subprocess.run([
        str(mono / 'bin/mcs'), '-target:library',
        '-out:' + str(Path(output, 'ProgressionApiProbe.dll')),
        '-r:' + str(mono / 'lib/mono/4.8-api/Facades/netstandard.dll'),
        *['-r:' + str(Path(output, name + '.dll')) for name in refs],
        *[str(p) for p in sorted(Path(__file__).parent.glob('*ApiProbe.cs'))],
    ])
    if result.returncode:
        raise SystemExit(result.returncode)
    print('API probe compiled successfully; no runtime test performed.')
