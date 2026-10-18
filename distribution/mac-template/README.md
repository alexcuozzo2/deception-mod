# Mac installer source

Exact `setup.py` and `setup.sh` from the verified Deception 0.3.100 kit, promoted from the local generated package into version control. No runtime binaries, mod archive, credentials or machine-specific installed configuration are included.

These scripts need the complete staged kit (`package-manifest.json`, `SHA256SUMS.txt`, `payload/`, `mod/`) and must not be run standalone. The combined package builder still takes an explicitly verified Mac package receipt and matching mod build receipt. This promotion does not rebuild or modify an existing ZIP.
