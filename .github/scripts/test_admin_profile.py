from pathlib import Path
import tempfile
import unittest

import admin_profile as profile


class ProfileTests(unittest.TestCase):
    def test_only_child_receives_jit_settings_and_workload_affinity_is_preserved(self):
        workload = ['taskset', '-c', '3', 'dotnet', '/product/app.dll', 'probe', 'legacy-delete:16', '/out', '480', '180']
        command = profile.capture_command('/tools/dotnet-trace', workload, '/out')
        boundary = command.index('--')
        self.assertFalse(any('DOTNET_Jit' in value for value in command[:boundary]))
        self.assertEqual(command[boundary + 1], 'env')
        self.assertEqual(command[-len(workload):], workload)
        self.assertIn('DOTNET_JitStdOutFile=' + str(Path('/out/jit.asm')), command)
        self.assertIn('Microsoft-Windows-DotNETRuntime:0x1:4', command[command.index('--providers') + 1])

    def test_missing_or_empty_trace_and_jit_output_cannot_succeed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for missing in ('runtime.nettrace', 'jit.asm'):
                for name in ('runtime.nettrace', 'jit.asm'):
                    (root / name).write_bytes(b'retained output')
                (root / missing).unlink()
                with self.assertRaisesRegex(ValueError, 'Missing diagnostic output'):
                    profile.validate_capture(root)
                (root / missing).touch()
                with self.assertRaisesRegex(ValueError, 'Missing diagnostic output'):
                    profile.validate_capture(root)
            (root / 'jit.asm').write_bytes(b'code')
            profile.validate_capture(root)


if __name__ == '__main__':
    unittest.main()
