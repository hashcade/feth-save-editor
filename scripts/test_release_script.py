#!/usr/bin/env python3
"""Check release delegation without Git or network writes."""

import contextlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import release


class ReleaseTests(unittest.TestCase):
    def test_patch_release_delegates_validation_to_ci(self):
        with tempfile.TemporaryDirectory(prefix="feth-release-test-") as directory:
            version = Path(directory) / "VERSION"
            version.write_text("0.3.1\n", encoding="utf-8")
            with patch.object(release, "VERSION_PATH", version), \
                    patch.object(release, "check_repository"), patch.object(release, "check_new_tag"), \
                    patch.object(release, "check_build") as check_build, \
                    patch.object(release, "command") as command, \
                    patch.object(release, "output", return_value="VERSION"), \
                    patch("sys.argv", ["release.py", "patch", "--yes"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                release.main()
            check_build.assert_not_called()
            self.assertEqual(version.read_text(encoding="utf-8"), "0.3.2\n")
            self.assertEqual([call.args for call in command.call_args_list], [
                ("git", "add", "VERSION"),
                ("git", "commit", "-m", "chore: release v0.3.2"),
                ("git", "push", "origin", "main"),
                ("git", "tag", "-a", "v0.3.2", "-m", "v0.3.2"),
                ("git", "push", "origin", "v0.3.2"),
            ])


if __name__ == "__main__":
    unittest.main()
