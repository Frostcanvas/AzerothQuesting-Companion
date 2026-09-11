from pathlib import Path

addon_service = Path('src/AzerothQuesting.Companion/AddonService.cs')
text = addon_service.read_text(encoding='utf-8')

old_guard = '''        if (IsWowRunning())
        {
            throw new InvalidOperationException("World of Warcraft is running. Close WoW before installing or updating the addon.");
        }
'''
new_guard = '''        var wowWasRunning = IsWowRunning();
        if (wowWasRunning)
        {
            progress?.Report("World of Warcraft is running. Updating the addon files on disk now; use /reload or relog after the update to load the new version.");
        }
'''
if old_guard not in text:
    raise SystemExit('Expected WoW-running install guard was not found; refusing to patch')
text = text.replace(old_guard, new_guard, 1)

old_install = '''            var sourceAddon = Path.GetDirectoryName(toc)!;
            if (Directory.Exists(targetAddon))
            {
                Directory.Delete(targetAddon, recursive: true);
            }

            CopyDirectory(sourceAddon, targetAddon);

            var installedVersion = GetInstalledVersion(retailPath);
'''
new_install = '''            var sourceAddon = Path.GetDirectoryName(toc)!;
            var stagedAddon = Path.Combine(addOnsPath, $".AzerothQuesting.update-{Guid.NewGuid():N}");
            try
            {
                CopyDirectory(sourceAddon, stagedAddon);
                InstallStagedDirectory(stagedAddon, targetAddon, progress);
            }
            finally
            {
                TryDeleteDirectory(stagedAddon);
            }

            var installedVersion = GetInstalledVersion(retailPath);
'''
if old_install not in text:
    raise SystemExit('Expected addon replacement block was not found; refusing to patch')
text = text.replace(old_install, new_install, 1)

old_progress = '''            progress?.Report($"Azeroth Questing {installedVersion} is installed.");
            return new AddonInstallResult(installedVersion, backupRoot);
'''
new_progress = '''            progress?.Report(wowWasRunning
                ? $"Azeroth Questing {installedVersion} is updated on disk. Use /reload or relog in WoW to load it."
                : $"Azeroth Questing {installedVersion} is installed.");
            return new AddonInstallResult(installedVersion, backupRoot);
'''
if old_progress not in text:
    raise SystemExit('Expected install completion status was not found; refusing to patch')
text = text.replace(old_progress, new_progress, 1)

insert_before = '''    private static void BackupDirectoryIfPresent(string source, string destination)
'''
helpers = '''    private static void InstallStagedDirectory(
        string stagedAddon,
        string targetAddon,
        IProgress<string>? progress)
    {
        var addOnsPath = Path.GetDirectoryName(targetAddon)
            ?? throw new InvalidOperationException("Could not determine the WoW AddOns folder.");
        var previousAddon = Path.Combine(addOnsPath, $".AzerothQuesting.previous-{Guid.NewGuid():N}");
        var targetMoved = false;

        try
        {
            if (Directory.Exists(targetAddon))
            {
                Directory.Move(targetAddon, previousAddon);
                targetMoved = true;
            }

            Directory.Move(stagedAddon, targetAddon);
            TryDeleteDirectory(previousAddon);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!Directory.Exists(targetAddon) && targetMoved && Directory.Exists(previousAddon))
            {
                try
                {
                    Directory.Move(previousAddon, targetAddon);
                }
                catch (Exception restoreEx)
                {
                    throw new IOException(
                        "The addon update could not swap folders and the previous addon folder could not be restored automatically. " +
                        "Use the Companion backup before retrying.",
                        new AggregateException(ex, restoreEx));
                }
            }

            progress?.Report("Windows could not swap the addon folder atomically; updating the addon files in place instead...");
            CopyDirectory(stagedAddon, targetAddon);
            TryDeleteDirectory(previousAddon);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The normal timestamped backup is already retained. A temporary
            // swap folder can be cleaned up by Windows or a later maintenance pass.
        }
    }

'''
if insert_before not in text:
    raise SystemExit('Expected helper insertion point was not found; refusing to patch')
text = text.replace(insert_before, helpers + insert_before, 1)
addon_service.write_text(text, encoding='utf-8')

csproj = Path('src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj')
text = csproj.read_text(encoding='utf-8')
if '<Version>0.1.9-beta.8</Version>' not in text or '<FileVersion>0.1.8.13</FileVersion>' not in text:
    raise SystemExit('Expected Companion Beta 8 project version was not found; refusing to patch')
text = text.replace('<Version>0.1.9-beta.8</Version>', '<Version>0.1.9-beta.9</Version>', 1)
text = text.replace('<FileVersion>0.1.8.13</FileVersion>', '<FileVersion>0.1.8.14</FileVersion>', 1)
csproj.write_text(text, encoding='utf-8')

installer = Path('installer/AzerothQuestingCompanion.iss')
text = installer.read_text(encoding='utf-8')
text = text.replace('#define MyAppVersion "0.1.9-beta.5"', '#define MyAppVersion "0.1.9-beta.9"', 1)
text = text.replace('#define MyAppFileVersion "0.1.8.10"', '#define MyAppFileVersion "0.1.8.14"', 1)
installer.write_text(text, encoding='utf-8')

readme = Path('README.md')
text = readme.read_text(encoding='utf-8')
text = text.replace('## Development version - v0.1.9-beta.7', '## Development version - v0.1.9-beta.9', 1)
text = text.replace('- Refuses to change addon files while World of Warcraft is running.', '- Can install, update, or repair Azeroth Questing while World of Warcraft is running; the running game keeps its already-loaded addon code until `/reload` or the next login.', 1)
text = text.replace('Addon updates are reported in the same check. Stable mode ignores GitHub prereleases; Beta mode considers both stable releases and GitHub prereleases and selects the newest compatible release. Use **Update Addon** to install the selected channel package. Addon updates are not applied while World of Warcraft is running.', 'Addon updates are reported in the same check. Stable mode ignores GitHub prereleases; Beta mode considers both stable releases and GitHub prereleases and selects the newest compatible release. Use **Update Addon** to install the selected channel package. The Companion may update addon files while World of Warcraft is running. WoW continues using the addon code already loaded in memory until the player uses `/reload` or logs out and back in.', 1)
readme.write_text(text, encoding='utf-8')

changelog = Path('CHANGELOG.md')
text = changelog.read_text(encoding='utf-8')
heading = '# Changelog\n\n'
if not text.startswith(heading):
    raise SystemExit('Unexpected CHANGELOG.md heading; refusing to patch')
if '## 0.1.9 Beta 9 -' in text:
    raise SystemExit('Companion Beta 9 changelog entry already exists; refusing duplicate')
entry = '''## 0.1.9 Beta 9 - September 11, 2026 - Available on GitHub Pre-release

- **Fixed** the Companion refusing every addon install/update whenever a World of Warcraft process was running. Azeroth Questing can now be installed, updated, or repaired on disk while Retail WoW remains open, matching normal addon-manager behavior more closely.
- **Changed** live-game update behavior so the running WoW session keeps using the addon code it already loaded. After the Companion finishes writing the new package, the player can use `/reload` when convenient or relog to activate the updated addon; the Companion no longer forces the entire game to close first.
- **Improved** addon replacement safety by fully staging the downloaded addon beside the live AddOns folder before activation, retaining the existing timestamped backup, attempting a directory swap first, and falling back to an in-place file copy if Windows prevents the atomic folder rename. The downloaded package is still validated by locating `AzerothQuesting.toc` before the installed folder is changed.
- **Updated** the Companion development documentation to describe live-WoW addon updates and the required `/reload`/relog activation step.
- **Changed** the Companion test build to `0.1.9-beta.9` with Windows file version `0.1.8.14` because Beta 8 had already been published and consumed before this updater behavior was requested.

This change affects the Companion updater only; no new Azeroth Questing addon, Website, or Azeroth Questing Server build is required. GitHub compilation/installer success does not count as Windows runtime or in-game testing. Test Beta 9 on Windows with Retail WoW open: update or repair Azeroth Questing, confirm the on-disk TOC changes without the close-WoW error, confirm the already-running UI does not pretend it hot-loaded the new code, then use `/reload` and verify the new addon version is active. Repeat once with WoW closed and verify backups and normal installation still work. If trusted signing remains unavailable, this Beta may be published as an explicitly unsigned testing build; Stable still requires trusted signing.

'''
changelog.write_text(heading + entry + text[len(heading):], encoding='utf-8')

Path('.github/workflows/apply-beta9-live-addon-update.yml').unlink()
Path('.github/apply_beta9_live_addon_update.py').unlink()
