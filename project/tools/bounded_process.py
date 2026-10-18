"""Run one build/test command in an owned, bounded process group.

This is for isolated project compiler/test commands, not application launchers.
It never enumerates or terminates unrelated processes.
"""
import os
import signal
import subprocess
import threading


def _signal_group(process, sig):
    try:
        os.killpg(process.pid, sig)
    except ProcessLookupError:
        pass


def _cleanup(process):
    # The group was created by this exact Popen. Kill descendants as well as the
    # leader, including when the leader has already exited. Do not rely on a
    # child respecting TERM or keeping its inherited stdout closed.
    _signal_group(process, signal.SIGTERM)
    try:
        process.wait(timeout=0.5)
    except subprocess.TimeoutExpired:
        pass
    finally:
        _signal_group(process, signal.SIGKILL)
        process.wait()


def run(args, *, timeout=180, **kwargs):
    """Return CompletedProcess; preserve nonzero return codes and captured output.

    Raise TimeoutExpired after cleaning the command's complete process group.
    KeyboardInterrupt and SIGTERM also clean the group before propagating. A
    successful command may not leave background descendants behind.
    """
    if os.name != "posix":
        raise RuntimeError("Bounded build/test groups require POSIX")
    if not isinstance(timeout, (int, float)) or isinstance(timeout, bool) or timeout <= 0:
        raise ValueError("A positive finite command timeout is required")
    if timeout == float("inf") or timeout != timeout:
        raise ValueError("A positive finite command timeout is required")
    for forbidden in ("start_new_session", "process_group", "preexec_fn", "check", "input", "capture_output"):
        if forbidden in kwargs:
            raise ValueError("Unsupported bounded process option: " + forbidden)
    process = None
    prior_term = None
    changed_term = False
    # Tool cancellation may send TERM instead of Ctrl-C. Default SIGTERM would
    # skip Python finally blocks and strand the new child session.
    if threading.current_thread() is threading.main_thread():
        prior_term = signal.getsignal(signal.SIGTERM)
        if prior_term == signal.SIG_DFL:
            def interrupted(signum, frame):
                raise KeyboardInterrupt("Build/test runner interrupted by SIGTERM")
            signal.signal(signal.SIGTERM, interrupted)
            changed_term = True
    try:
        process = subprocess.Popen(args, start_new_session=True, **kwargs)
        stdout, stderr = process.communicate(timeout=timeout)
        return subprocess.CompletedProcess(process.args, process.returncode, stdout, stderr)
    finally:
        try:
            if process is not None:
                try:
                    _cleanup(process)
                finally:
                    for stream in (process.stdin, process.stdout, process.stderr):
                        if stream is not None:
                            stream.close()
        finally:
            if changed_term:
                signal.signal(signal.SIGTERM, prior_term)
