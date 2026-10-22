"""Reproduce the prototype's authored JSON from local exported native templates.
No game files, saves or installed mod configuration are written by this tool.
"""
import copy
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[1]
EXPORT = ROOT.parent / 'mac-modding/RogueTrader-Modding/Blueprints'
DEST = ROOT / 'src/Prototype'
NS = uuid.UUID('4fd6803b-0c2e-4acf-9bc0-ff665f5a8cf1')
def stable(name): return uuid.uuid5(NS, name).hex
IDS = {n: stable(n) for n in ['quest','dialog','start-actions','available','scene','entity',
    'mechanics-deck','mechanics-cabin','mechanics-after'] +
    ['page'+str(i) for i in range(1,4)] + ['cue'+str(i) for i in range(1,4)] +
    ['next'+str(i) for i in range(1,3)] + ['ack','cancel']}
IDS.update(objective='b72f12cf229b501ca5fef51a60373d6b', etude='c9d6c0e2392251cb89eadb2e908b2f41')
AREAS = ['05e7294985fa4d81b4c133f648256ed6','76b853bf2d3940228c2af55ca3c4ac1d','66b9351d457f4363afebcbca96a506da']
STRINGS = {}
def bp(n): return '!bp_'+IDS[n]
def template(p): return json.loads((EXPORT/p).read_text())['Data']
def loc(key,text):
    key='eisenhorn-prototype-'+key
    STRINGS[key]={'Offset':0,'Text':text}
    return dict(m_Key=key,m_OwnerString='',m_OwnerPropertyPath='',m_JsonPath='',Shared=None)
def write(name,d):
    d=copy.deepcopy(d);d['Author']='Poffl';d['Comment']='Eisenhorn quest prototype 0.1.0';d['PrototypeLink']='';d['m_Overrides']=[]
    (DEST/'Blueprints'/f'{name}.jbp.meta').write_text('fileFormatVersion: 2\nguid: '+IDS[name]+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    (DEST/'Blueprints'/f'{name}.jbp').write_text(json.dumps(dict(AssetId=IDS[name],Data=d,Meta={'ShadowDeleted':False}),indent=2)+'\n')
def checker(*cs):return dict(Operation='And',Conditions=list(cs))
def objective_condition(state):return {'$type':'e77a58dedc8350a4faa55f5e66b9f575, ObjectiveStatus','name':'$ObjectiveStatus$'+stable('condition-'+state),'Not':False,'m_QuestObjective':bp('objective'),'State':state}
def action(typ,**fields): return {'$type':typ,'name':'$'+typ.split(', ')[-1]+'$'+stable(json.dumps(fields,sort_keys=True)),**fields}
empty={'Actions':[]}
q=template('World/Quests/Chapter 0/Prologue_quest.jbp')
q.update(Components=[],Title=loc('quest-title','Eisenhorn: Quest System Test'),Description=loc('quest-description','Read the test document to verify the native quest and Book dialogue systems.'),CompletionText=loc('quest-complete','The Eisenhorn quest prototype was acknowledged.'),m_Objectives=[bp('objective')],m_LastChapter=5)
for k in ['RegionName','Place','ServiceMessage']:q[k]=loc(k,'')
write('quest',q)
o=template('World/Quests/Chapter 0/03_TalkToTheodora.jbp')
o.update(Components=[],Title=loc('objective-title','Read the Eisenhorn test document'),Description=loc('objective-description','After the out-of-control servitor encounter, leave the conversation and look for the parchment at your character’s feet. It is also available in the prologue cabin/bridge area and the post-prologue officer deck. Select the final acknowledgement to finish this test.'),Destination=loc('destination','Voidship'),m_Quest=bp('quest'),m_FinishParent=True,m_Hidden=False,m_NextObjectives=[],m_Areas=['!bp_'+a for a in AREAS])
write('objective',o)
et=template('World/Etudes/RogueTraderEtude/Chapter01_Extra/AuroraMurdered.jbp')
trigger=template('World/Etudes/RogueTraderEtude/Prologue/Prologue_Stages/Prologue_Start.jbp')['Components'][0]
trigger.update(name='$EtudePlayTrigger$'+stable('give-trigger'),Conditions=checker(objective_condition('None')),Actions={'Actions':[action('3af21dd61b640c941b5704f3df91e16d, GiveObjective',m_Objective=bp('objective'))]})
et.update(Components=[trigger],m_Parent='',m_LinkedAreaPart=None,m_StartsWith=[],m_StartsOnComplete=[],m_AddedAreaMechanics=[bp(n) for n in ['mechanics-deck','mechanics-cabin','mechanics-after']],ActivationCondition=checker({'$type':'e77a58dedc8350a4faa55f5e66b9f575, ObjectiveStatus','name':'$ObjectiveStatus$'+stable('milestone-condition'),'Not':False,'m_QuestObjective':'!bp_b653839065a647a08b5721e077da23e1','State':'Completed'}),CompletionCondition=checker())
write('etude',et)
for n,a in zip(['mechanics-deck','mechanics-cabin','mechanics-after'],AREAS):
 d=template('World/Areas/Chapter_1/StarPort/Addons/StarPort_SecondState.jbp')
 d.update(Area='!bp_'+a,Scene={'m_SceneAsset':{'guid':IDS['scene'],'fileid':102900000},'m_SceneName':'EisenhornPrototype_Mechanics'})
 write(n,d)
d=template('World/Dialogs/Ch2_VS/ForgeWorld/Scrapcode_bookevent/Scrapcode_bookevent.jbp')
d.update(FirstCue={'Cues':[bp('page1')],'Strategy':'First'},StartActions=empty,FinishActions=empty,ReplaceActions=empty,Conditions=checker(objective_condition('Started')),TurnPlayer=False,TurnFirstSpeaker=False,Type='Book',Description=None)
write('dialog',d)
page=template('World/Test/TestBE2/BookPage_0001.jbp')
cue=template('World/Test/TestBE2/Cue_0001.jbp')
answer=template('Test/Exploration/Dialogs/Trinnitos_steppes/Answer_0001.jbp')
for i,text in enumerate(['Test Page 1 – Eisenhorn Quest System','Test Page 2 – Story Progression','Test Page 3 – Quest Complete'],1):
 p=copy.deepcopy(page);p.update(Components=[],ParentAsset=IDS['dialog'],Cues=[bp('cue'+str(i))],Answers=[bp('next'+str(i)) if i<3 else bp('ack'),bp('cancel')],OnShow=empty,ShowMainCharacter=False,ShowMainCharacterName=False,Portrait=None)
 write('page'+str(i),p)
 c=copy.deepcopy(cue);c.update(Components=[],ParentAsset=IDS['dialog'],Text=loc('page'+str(i),text),ShowOnce=False,ShowOnceCurrentDialog=False,Conditions=checker(),OnShow=empty,OnStop=empty,Answers=[],Continue={'Cues':[],'Strategy':'First'},Animation='None',IsNarratorText=True)
 c['Speaker'].update(NoSpeaker=True,MoveCamera=False)
 write('cue'+str(i),c)
for name,text,nextname in [('next1','Read the next page.','page2'),('next2','Read the next page.','page3'),('ack','Acknowledge and complete the quest.',None),('cancel','Close the document. I will return later.',None)]:
 a=copy.deepcopy(answer);a.update(Components=[],ParentAsset=IDS['dialog'],Text=loc(name,text),NextCue={'Cues':[bp(nextname)] if nextname else [],'Strategy':'First'},ShowOnce=False,ShowOnceCurrentDialog=False,ShowConditions=checker(),SelectConditions=checker(objective_condition('Started')),OnSelect=empty,RequireValidCue=False)
 if name=='ack':a['OnSelect']={'Actions':[action('31abd123cda0b4147b509219dbb097a0, SetObjectiveStatus',Status='Complete',m_Objective=bp('objective'),StartObjectiveIfNone=False)]}
 write(name,a)
h=template('World/Encounters/DestroyedShipBESystem/ActionsHolders/StartBookEvent.jbp')
h['Actions']={'Actions':[action('0e0c1fe99d7862d4492bcd1535939a9a, StartDialog',DialogueOwner=None,m_Dialogue=bp('dialog'),DialogEvaluator=None,SpeakerName=loc('speaker','Eisenhorn Test Document'))]}
write('start-actions',h)
h=template('World/Encounters/VoidshipOfficersDeck/ConditionsHolders/KunradBarksAfterBattle.jbp');h['Conditions']=checker(objective_condition('Started'));write('available',h)
for locale in ['enGB','deDE','frFR','ruRU','esES','zhCN']:
 (DEST/'Localization'/f'{locale}.json').write_text(json.dumps({'strings':STRINGS},indent=2,ensure_ascii=False)+'\n')
(DEST/'StableIds.json').write_text(json.dumps(IDS,indent=2)+'\n')
# Persist Unity script and asset GUIDs in source rather than generating them at import.
for p in (DEST/'Scripts').iterdir():
 if p.suffix in ['.cs','.asmdef']:
  importer='MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter'
  (p.parent/(p.name+'.meta')).write_text('fileFormatVersion: 2\nguid: '+stable(p.name)+'\n'+importer+':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('Generated',len(list((DEST/'Blueprints').glob('*.jbp'))),'native blueprints; stable scene GUID',IDS['scene'])
