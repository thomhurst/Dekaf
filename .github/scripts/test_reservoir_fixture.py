import tempfile
import unittest
from pathlib import Path
from reservoir_fixture import validate


class ReservoirFixtureTests(unittest.TestCase):
    def test_only_intended_upgrade_is_allowed_and_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            a, b = Path(temporary) / 'A', Path(temporary) / 'B'
            a.mkdir()
            b.mkdir()
            original = b'<PackageVersion Include="Reservoir" Version="1.6.7" />'
            upgraded = original.replace(b'1.6.7', b'1.7.0')
            (a / 'Directory.Packages.props').write_bytes(original)
            (b / 'Directory.Packages.props').write_bytes(upgraded)
            validate(b, a)
            self.assertEqual(original, (a / 'Directory.Packages.props').read_bytes())
            self.assertEqual(upgraded, (b / 'Directory.Packages.props').read_bytes())
            for invalid in [original, upgraded + b'extra', upgraded.replace(b'1.7.0', b'1.7.1')]:
                (b / 'Directory.Packages.props').write_bytes(invalid)
                with self.assertRaises(ValueError):
                    validate(b, a)
            (b / 'Directory.Packages.props').write_bytes(upgraded)
            (b / 'global.json').write_text('{}')
            with self.assertRaises(ValueError):
                validate(b, a)
