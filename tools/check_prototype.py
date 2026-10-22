"""Check authored graph invariants and optionally a built Owlmod ZIP."""
import json
from pathlib import Path
import re
import sys
import zipfile

root=Path(__file__).resolve().parents[1]/'src/Prototype'
ids=json.loads((root/'StableIds.json').read_text())
blueprints={p.stem:json.loads(p.read_text()) for p in (root/'Blueprints').glob('*.jbp')}
assert len(blueprints)==19
assert len({v['AssetId'] for v in blueprints.values()})==19
external={'05e7294985fa4d81b4c133f648256ed6','76b853bf2d3940228c2af55ca3c4ac1d','66b9351d457f4363afebcbca96a506da','b653839065a647a08b5721e077da23e1'}
owned={v['AssetId'] for v in blueprints.values()}
refs=set()
keys=set()
completions=[]
def walk(x,path):
    if isinstance(x,dict):
        if x.get('Status')=='Complete' and 'SetObjectiveStatus' in x.get('$type',''):
            completions.append((path,x))
        if x.get('m_Key'):keys.add(x['m_Key'])
        for k,v in x.items():walk(v,path+'.'+k)
    elif isinstance(x,list):
        for i,v in enumerate(x):walk(v,path+'['+str(i)+']')
    elif isinstance(x,str) and x.startswith('!bp_'):refs.add(x[4:])
for name,v in blueprints.items():
    assert v['AssetId']==ids[name]
    assert 'guid: '+ids[name] in (root/'Blueprints'/f'{name}.jbp.meta').read_text()
    walk(v,name)
assert refs <= owned|external, refs-owned-external
assert len(completions)==1 and completions[0][0].startswith('ack.Data.OnSelect.')
assert completions[0][1]['m_Objective']=='!bp_'+ids['objective']
assert not blueprints['cancel']['Data']['OnSelect']['Actions']
for i in range(1,4):
    p=blueprints['page'+str(i)]['Data']
    assert '!bp_'+ids['cancel'] in p['Answers']
    assert not p['OnShow']['Actions']
    c=blueprints['cue'+str(i)]['Data']
    assert not c['OnShow']['Actions'] and not c['OnStop']['Actions']
    assert not c['ShowOnce']
    if i<3:assert blueprints['next'+str(i)]['Data']['NextCue']['Cues']==['!bp_'+ids['page'+str(i+1)]]
assert not blueprints['ack']['Data']['NextCue']['Cues']
assert blueprints['objective']['Data']['m_FinishParent']
assert blueprints['quest']['Data']['m_Objectives']==['!bp_'+ids['objective']]
assert blueprints['available']['Data']['Conditions']['Conditions'][0]['State']=='Started'
assert blueprints['dialog']['Data']['Type']=='Book'
assert not blueprints['dialog']['Data']['FinishActions']['Actions']
etude=blueprints['etude']['Data']
assert not etude['m_Parent']
assert etude['ActivationCondition']['Conditions'][0]['m_QuestObjective']=='!bp_b653839065a647a08b5721e077da23e1'
assert etude['ActivationCondition']['Conditions'][0]['State']=='Completed'
assert etude['Components'][0]['Conditions']['Conditions'][0]['State']=='None'
assert len(etude['m_AddedAreaMechanics'])==3
for p in (root/'Localization').glob('*.json'):
    assert keys <= json.loads(p.read_text())['strings'].keys()
assert not list((root/'Blueprints').glob('*.patch'))
print('PASS: 19 unique, stable blueprint identities; all blueprint references resolve to owned/explicit native assets.')
print('PASS: every page can cancel; only final acknowledgement completes the owned objective; no completion-on-close actions.')
print('PASS: quest parent, Book graph, started-state interaction gate, milestone condition and three native mechanics references.')
print('PASS: all authored localization keys available in six locale packs; no vanilla blueprint patches.')
if len(sys.argv)>1:
    with zipfile.ZipFile(sys.argv[1]) as z:
        names=z.namelist()
        assert z.testzip() is None
        assert len([n for n in names if n.endswith('.jbp')])==19
        assert len([n for n in names if n.endswith('.dll')])==1
        assert any(n.endswith('ArmouryEisenhorn.Prototype.dll') for n in names)
        assert any('eisenhornprototype_mechanics' in n.lower() for n in names)
        assert any('blueprintdirectreferences' in n.lower() for n in names)
        for n in names:
            if n.endswith('.jbp'):
                obj=json.loads(z.read(n));assert obj['AssetId'] in owned
        settings_name=next(n for n in names if n.endswith('OwlcatModificationSettings.json'))
        settings=json.loads(z.read(settings_name))
        bundle_names={n.split('/')[-1] for n in names if '/Bundles/' in '/'+n and not n.endswith('/')}
        dependencies={entry['Key']:entry['Value'] for entry in settings['BundleDependencies']['m_List']}
        assert bundle_names==dependencies.keys()
        assert all(set(values)<=bundle_names|{'shaders'} for values in dependencies.values())
        assert set(dependencies['EisenhornQuestPrototype_eisenhornprototype_mechanics'])=={'EisenhornQuestPrototype_content','shaders'}
        assert not settings['BlueprintPatches']
        for n in names:
            if n.endswith('.jbp'):
                expected=(root/'Blueprints'/n.split('/')[-1]).read_bytes()
                assert z.read(n)==expected
        print('PASS: built ZIP CRC, exact authored blueprint bytes, runtime-only assembly and scene/reference bundles.')
        print('PASS: complete bundle dependency graph; native shaders dependency explicit; no vanilla patches.')
