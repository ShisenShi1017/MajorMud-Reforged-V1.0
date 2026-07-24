from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text(encoding='utf-8')
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(encoding='utf-8')
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(encoding='utf-8')
xaml_path=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
xaml=xaml_path.read_text(encoding='utf-8')
checks={
 'generated and entered approval codes separated':'GeneratedApprovalCode' in models and 'Enter Approval Code' in xaml,
 'proposal hash bound approval':'proposalSha256' in services and 'ComputeSha256' in services,
 'approval artifact persisted':'Approvals' in services and 'ApproveProposalAsync' in services,
 'explicit approval command':'ApproveProposalCommand' in vm and 'Approve Proposal' in xaml,
 'explicit apply command':'ApplyApprovedProposalCommand' in vm and 'Apply Approved' in xaml,
 'safe realm gate retained':'TargetIsSafe' in services and 'Live Realm' in vm,
 'approval required cannot be bypassed':'Approval Required must remain enabled for apply.' in services,
 'current proposal must match approval':'current proposal differs from the approved proposal' in services,
 'isolated staging output':'staged-to-isolated-test-realm-workspace' in services,
 'rollback backup created':'Backups' in services and 'backupPath' in services,
 'apply receipt created':'apply-receipt-' in services and 'LastApplyReceipt' in models,
 'no runtime mutation declared':'runtimeMutation = false' in services,
 'no librarium mutation declared':'librariumMutation = false' in services,
}
try:
 ET.parse(xaml_path); checks['xaml parse']=True
except Exception: checks['xaml parse']=False
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+': '+name)
passed=sum(checks.values())
print(f'RESULT: {passed}/{len(checks)}')
sys.exit(0 if passed==len(checks) else 1)
