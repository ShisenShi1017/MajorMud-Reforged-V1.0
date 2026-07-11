import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
server=root/'Apps/Source/Reforged.Server'
required={
 'RuntimeWorldMultiplayer.cs':['RuntimeWorldInteractionEngine','RuntimeMovementEngine','RuntimePartyRegistry','RuntimeReputationState','RuntimeCommunicationRouter','RuntimeWorldSchedulerState','RuntimeWorldMultiplayerValidator','RuntimeWorldMultiplayerPersistence'],
 'RuntimeWorldMultiplayerSmokeReport.cs':['RuntimeWorldMultiplayerSmokeReport','world-interaction','movement','party','reputation','communication','scheduler','persistence','validator','rollback'],
 'Program.cs':['--smoke-world-multiplayer','RuntimeWorldMultiplayerSmokeReport.Run']
}
errors=[]
for name,tokens in required.items():
 p=server/name
 if not p.exists(): errors.append('missing:'+str(p.relative_to(root))); continue
 text=p.read_text(encoding='utf-8')
 for token in tokens:
  if token not in text: errors.append(f'missing-token:{name}:{token}')
for banned in ['Data/Classic','E:\\MajorMud-Reforged Github','room.2147']:
 text=(server/'RuntimeWorldMultiplayer.cs').read_text(encoding='utf-8')
 if banned in text: errors.append('hardcoded-runtime-value:'+banned)
print(json.dumps({'passed':not errors,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
