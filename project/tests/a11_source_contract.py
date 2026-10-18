"""A11 explicitly revised two source files; preserve historical receipts independently."""
import hashlib,json

def assert_a11_source(test,root,name,historical=None):
    amendment=json.loads((root/'evidence/a11-source-amendment.json').read_text())
    test.assertEqual(hashlib.sha256((root/amendment['authority']).read_bytes()).hexdigest(),amendment['authority_sha256'])
    if historical:
        old=json.loads((root/historical).read_text())['source_sha256'][name]
        test.assertEqual(old,amendment['historical'][historical][name])
    expected=amendment['sources'][name]
    if name=='src/Core/TechTree.cs':
        current=json.loads((root/'evidence/a52-source-amendment.json').read_text())
        test.assertEqual(hashlib.sha256((root/current['authority']).read_bytes()).hexdigest(),current['authority_sha256'])
        test.assertEqual(current['sources'][name]['previous_sha256'],expected)
        expected=current['sources'][name]['source_sha256']
        current=json.loads((root/'evidence/a64-source-amendment.json').read_text())
        test.assertEqual(hashlib.sha256((root/current['authority']).read_bytes()).hexdigest(),current['authority_sha256'])
        test.assertEqual(current['sources'][name]['previous_sha256'],expected)
        expected=current['sources'][name]['source_sha256']
    test.assertEqual(hashlib.sha256((root/name).read_bytes()).hexdigest(),expected)
