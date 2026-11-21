import json
from pathlib import Path
import tempfile
import unittest
from verify import export_summary
class PublicReportTests(unittest.TestCase):
    def test_public_summary_omits_hostnames_paths_and_raw_process_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);raw=root/'raw';raw.mkdir();public=root/'public'
            (raw/'verification.json').write_text(json.dumps({'identity':{'version':'0.1.0','revision':'a'*40,'official':True,'dirty':False},'checks':[{'check':'UnitTests','exitCode':0}],'complete':True}))
            (raw/'UnitTests.trx').write_text('<TestRun computerName="private-host"><ResultSummary><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0" /></ResultSummary><UnitTestResult storage="/home/operator/private/repo" /></TestRun>')
            (raw/'UnitTests.log').write_text('private secret diagnostic')
            export_summary(raw,public)
            text=(public/'verification.json').read_text()
            self.assertNotIn('private',text);self.assertNotIn('/home',text);self.assertNotIn('computerName',text)
            self.assertEqual(json.loads(text)['tests'][0]['passed'],2)
            self.assertEqual([p.name for p in public.iterdir()],['verification.json'])
