"""Document/diagram regressions for material independent018 findings.

This checks the design contract, not a deployed Android application.
"""
import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parent / 'design-c4'

class Design018Contract(unittest.TestCase):
    def setUp(self):
        self.doc = (ROOT / 'docs/Android MCP Bridge Design.md').read_text()
        self.model = (ROOT / 'architecture/workspace.dsl').read_text()
        self.code = (ROOT / 'architecture/code-diagrams.md').read_text()

    def test_callback_state_machine_has_terminal_cleanup_semantics(self):
        for trigger in ('target_died', 'lease_expired', 'registration_timeout'):
            self.assertIn(trigger, self.doc)
        self.assertIn('cleanup_disposition', self.doc)
        self.assertIn('+onCalleeDied(token)', self.code)

    def test_inflight_distinguishes_admission_from_dispatch(self):
        self.assertIn('admitted but not dispatched', self.doc)
        self.assertIn('dispatched unary', self.doc)
        self.assertNotIn('a contract, capability or authority change revalidates or rejects it', self.doc)

    def test_direct_event_extension_has_negotiation_and_fallback(self):
        for item in ('android.bridge.directEvents.v1', '/v1/subscriptions/{subscription_id}/events', 'DirectStreamNegotiator'):
            self.assertIn(item, self.doc + self.code)

    def test_models_include_arc_and_owner_sequences(self):
        self.assertIn('dynamic bridge.app ArcSession', self.model)
        self.assertIn('dynamic bridge.app OwnerTransition', self.model)

    def test_all_screen_definitions_show_running_status(self):
        manifest = json.loads((ROOT / 'docs/ui-asset-manifest.json').read_text())
        for source in (ROOT / 'ui-svg').glob('SCR*.svg'):
            self.assertIn('data-status-id="' + source.stem + '.STATUS"', source.read_text())
        self.assertTrue(manifest)

if __name__ == '__main__':
    unittest.main()
