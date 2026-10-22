"""Compile-only check against installed assemblies; Unity builds the shipped package."""
import subprocess,tempfile,sys
from pathlib import Path
m=Path(sys.argv[1])
mono=Path(sys.argv[2])
refs=['Code','RogueTrader.GameCore','UnityEngine.CoreModule','Owlcat.Runtime.Core','Newtonsoft.Json','Kingmaker.PubSubSystem.Core.Interfaces','StateHasher','Kingmaker.UI.Models.Tooltip.Base','Kingmaker.Blueprints.Base','Kingmaker.Blueprints.JsonSystem.Hepers','Kingmaker.ElementsSystem.Interfaces','Kingmaker.ResourceLinks.BaseInterfaces','UniRx','Owlcat.Runtime.Visual','Kingmaker.EntitySystem.Interfaces','UnityEngine.PhysicsModule']
with tempfile.TemporaryDirectory() as t:
 r=[]
 for n in refs:
  if (m/(n+'.dll')).exists():
   p=Path(t,n+'.dll');p.symlink_to(m/(n+'.dll'));r+=['-r:'+str(p)]
 subprocess.run([str(mono/'bin/mcs'),'-target:library','-out:'+t+'/ArmouryEisenhorn.Prototype.dll','-r:'+str(mono/'lib/mono/4.8-api/Facades/netstandard.dll'),*r,*map(str,(Path(__file__).resolve().parents[1]/'src/Prototype/Scripts').glob('*.cs'))],check=True)

print("Prototype runtime compiled against installed game assemblies; no game execution.")
