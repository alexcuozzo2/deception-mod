import importlib.util
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

TOOLS = Path(__file__).resolve().parents[1] / "tools"
spec = importlib.util.spec_from_file_location("bounded_process", TOOLS / "bounded_process.py")
bounded = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bounded)


class BoundedProcessTests(unittest.TestCase):
    def test_completed_output_and_nonzero_code(self):
        result = bounded.run([sys.executable, "-c", "print('retained');raise SystemExit(7)"],
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        self.assertEqual(result.returncode, 7)
        self.assertEqual(result.stdout, "retained\n")

    def test_invalid_unbounded_or_foreign_group_options(self):
        for timeout in (0, -1, float("inf"), float("nan"), True):
            with self.assertRaises(ValueError):
                bounded.run([sys.executable], timeout=timeout)
        with self.assertRaises(ValueError):
            bounded.run([sys.executable], start_new_session=False)

    @staticmethod
    def alive(pid):
        # kill(0) considers an unreaped zombie alive; inspect only the exact test
        # child PID and accept either death or zombie waiting for its reaper.
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return False
        status = subprocess.run(["/bin/ps", "-o", "stat=", "-p", str(pid)],
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        if status.returncode not in (0, 1) or (status.returncode != 0 and status.stderr.strip()):
            raise RuntimeError("Exact test-child status could not be inspected: " + status.stderr.strip())
        return bool(status.stdout.strip()) and not status.stdout.strip().startswith("Z")

    def assert_gone(self, pid):
        for _ in range(50):
            if not self.alive(pid):
                return
            time.sleep(.02)
        self.fail("Owned test child still running: " + str(pid))

    @staticmethod
    def sleeper(path, normal_exit=False):
        # Grandchild deliberately ignores TERM, requiring the group KILL path.
        return ("import subprocess,sys,time,pathlib;"
                "p=subprocess.Popen([sys.executable,'-c','import signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);time.sleep(60)'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);"
                f"pathlib.Path({str(path)!r}).write_text(str(p.pid));"
                + ("time.sleep(.1)" if normal_exit else "time.sleep(60)"))

    def test_timeout_kills_descendant(self):
        with tempfile.TemporaryDirectory() as d:
            child = Path(d) / "child"
            with self.assertRaises(subprocess.TimeoutExpired):
                bounded.run([sys.executable, "-c", self.sleeper(child)], timeout=.5,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            self.assert_gone(int(child.read_text()))

    def test_success_cleans_background_descendant(self):
        with tempfile.TemporaryDirectory() as d:
            child = Path(d) / "child"
            result = bounded.run([sys.executable, "-c", self.sleeper(child, True)], timeout=3)
            self.assertEqual(result.returncode, 0)
            self.assert_gone(int(child.read_text()))

    def test_term_interrupt_cleans_descendant(self):
        with tempfile.TemporaryDirectory() as d:
            child = Path(d) / "child"
            code = (f"import sys;sys.path.insert(0,{str(TOOLS)!r});import bounded_process;"
                    f"bounded_process.run([sys.executable,'-c',{self.sleeper(child)!r}],timeout=10)")
            runner = subprocess.Popen([sys.executable, "-c", code], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                for _ in range(100):
                    if child.exists():
                        break
                    time.sleep(.02)
                self.assertTrue(child.exists())
                runner.send_signal(signal.SIGTERM)
                runner.communicate(timeout=5)
                self.assertNotEqual(runner.returncode, 0)
                self.assert_gone(int(child.read_text()))
            finally:
                if runner.poll() is None:
                    runner.kill()
                runner.wait()


if __name__ == "__main__":
    unittest.main()
