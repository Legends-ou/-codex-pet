# Release process

1. Run the full test suite from the repository root.
2. Pick the intended semantic version, for example `2.0.0`.
3. Build the installer and portable archive:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1 -Version 2.0.0
   ```

4. Verify the package contents and checksums:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Release.ps1 -Version 2.0.0
   ```

5. Smoke-test both the installer and portable archive on Windows.
6. Create a GitHub Release and attach the setup EXE, portable ZIP, and SHA-256 manifest.

Release files remain outside Git because the portable package may exceed GitHub's
regular file-size limit.
