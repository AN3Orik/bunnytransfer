# Release Process

## Automatic Release via GitHub Actions

### Creating a New Release

1. **Update version** in your code if needed
2. **Commit all changes**:
   ```bash
   git add .
   git commit -m "Prepare release v1.0.0"
   ```

3. **Create and push a version tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

4. **GitHub Actions will automatically**:
   - Build native binaries for all platforms:
     - Windows x64
     - Linux x64
     - macOS ARM64 (Apple Silicon)
     - macOS x64 (Intel)
   - Create a GitHub release
   - Upload all binaries as release assets

### Version Tag Format

Use semantic versioning: `v<major>.<minor>.<patch>`

Examples:
- `v1.0.0` - First stable release
- `v1.1.0` - New features added
- `v1.1.1` - Bug fixes
- `v2.0.0` - Breaking changes

## Manual Release (if needed)

If you need to create a release manually:

### 1. Build for all platforms

```bash
# Windows
.\build_win.bat

# Linux (on Linux machine or WSL)
./build_linux.sh

# macOS (on macOS machine)
./build_macos.sh
```

### 2. Create GitHub Release

1. Go to your repository on GitHub
2. Click "Releases" → "Create a new release"
3. Tag version: `v1.0.0`
4. Release title: `Release v1.0.0`
5. Describe changes in the description
6. Attach binary files:
   - `bunnytransfer-win-x64.exe`
   - `bunnytransfer-linux-x64`
   - `bunnytransfer-macos-arm64`
   - `bunnytransfer-macos-x64`
7. Click "Publish release"

## CI/CD Workflows

### Build Workflow

**File**: `.github/workflows/build.yml`

**Triggers**: 
- Push to main/master/develop branches
- Pull requests to main/master

**Purpose**: Continuous integration - ensures code builds on all platforms

### Release Workflow

**File**: `.github/workflows/release.yml`

**Triggers**: 
- Push of version tags (v*.*.*)

**Purpose**: Automated release creation and binary distribution

## Monitoring Builds

1. Go to "Actions" tab in your GitHub repository
2. See build status for each workflow run
3. Download build artifacts if needed
4. Check logs if build fails

## Troubleshooting

### Build fails on specific platform

Check the workflow logs in GitHub Actions to see platform-specific errors.

### Tag already exists

```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push origin :refs/tags/v1.0.0

# Create new tag
git tag v1.0.0
git push origin v1.0.0
```

### Release not created

Ensure:
1. Tag format is correct (`v*.*.*`)
2. GitHub Actions have write permissions
3. Check Actions tab for error messages
