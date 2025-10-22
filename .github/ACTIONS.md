# GitHub Actions Setup Guide

This repository includes automated CI/CD workflows for building and releasing BunnyTransfer.NET.

## Workflows

### 1. Build Workflow (Continuous Integration)

**File**: `.github/workflows/build.yml`

Automatically builds the project for all platforms on every push or pull request.

**Platforms**:
- Windows x64
- Linux x64
- macOS ARM64 (Apple Silicon)
- macOS x64 (Intel)

**Artifacts**: Build artifacts are available for download in the Actions tab.

### 2. Release Workflow (Automated Releases)

**File**: `.github/workflows/release.yml`

Creates releases with pre-built binaries when you push a version tag.

## How to Create a Release

### Step 1: Prepare the code

Make sure all your changes are committed:

```bash
git add .
git commit -m "Prepare release v1.0.0"
git push
```

### Step 2: Create and push a version tag

```bash
# Create a tag (use semantic versioning)
git tag v1.0.0

# Push the tag to GitHub
git push origin v1.0.0
```

### Step 3: Wait for automation

GitHub Actions will automatically:
1. ✅ Build native binaries for all platforms
2. ✅ Create a new GitHub release
3. ✅ Upload all binaries as release assets

### Step 4: Check the release

1. Go to your repository on GitHub
2. Navigate to "Releases"
3. Your new release should appear with all binaries attached

## Version Tag Format

Use semantic versioning with a `v` prefix:

- `v1.0.0` - Major release
- `v1.1.0` - Minor update (new features)
- `v1.1.1` - Patch (bug fixes)
- `v2.0.0` - Breaking changes

## Monitoring Builds

### Check Build Status

1. Go to the "Actions" tab in your GitHub repository
2. Click on the workflow run you want to check
3. View logs for each platform build

### Download Build Artifacts

For non-release builds, you can download artifacts:

1. Go to "Actions" tab
2. Click on a completed workflow run
3. Scroll down to "Artifacts"
4. Download `bunnytransfer-<platform>` artifacts

## Permissions

The release workflow requires write permissions to create releases. This is configured in the workflow file:

```yaml
permissions:
  contents: write
```

GitHub Actions should have this permission by default, but if releases fail:

1. Go to repository Settings
2. Actions → General
3. Workflow permissions
4. Select "Read and write permissions"
5. Save

## Troubleshooting

### Release not created

**Check**:
- Tag format is correct (`v*.*.*`)
- Tag was pushed to GitHub
- Workflow permissions are correct
- Check "Actions" tab for error messages

### Build fails

**Check**:
- All dependencies are available
- .NET 9 SDK is properly configured
- Check platform-specific logs in Actions tab

### Wrong binary names

The workflow copies binaries with specific names:
- `bunnytransfer-win-x64.exe`
- `bunnytransfer-linux-x64`
- `bunnytransfer-macos-arm64`
- `bunnytransfer-macos-x64`

## Local Testing

Before pushing a tag, you can test builds locally:

```bash
# Windows
.\build_win.bat

# Linux
./build_linux.sh

# macOS
./build_macos.sh

# Universal (auto-detect)
./build.sh
```

## Manual Release (Alternative)

If you prefer manual releases:

1. Build binaries locally for all platforms
2. Go to GitHub → Releases → "Create a new release"
3. Choose a tag or create a new one
4. Upload binaries manually
5. Publish

## Benefits of Automated Releases

✅ **Consistency** - Same build environment for all platforms
✅ **Speed** - Parallel builds for all platforms
✅ **Reliability** - No manual steps, less human error
✅ **Traceability** - Full build logs available
✅ **Convenience** - Just push a tag, rest is automatic
