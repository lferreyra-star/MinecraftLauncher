import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Services/ModInstallService.cs'
s = read(p)
old = '''        bool includeOptionalDependencies,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.Id))'''
new = '''        bool includeOptionalDependencies,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken,
        ModrinthVersion? selectedVersion = null)
    {
        if (string.IsNullOrWhiteSpace(project.Id))'''
assert old in s
s = s.replace(old, new, 1)

old = '''        var versions = await _modrinth.GetVersionsAsync(project.Id, gameVersion, loader, cancellationToken);
        var target = versions.FirstOrDefault(v => ModrinthService.IsCompatible(v, gameVersion, loader));
        if (target is null)
            throw new InvalidOperationException($"No hay una versión de '{project.Name}' compatible con {gameVersion} y {loader}.");
'''
new = '''        var versions = await _modrinth.GetVersionsAsync(project.Id, gameVersion, loader, cancellationToken);
        var target = selectedVersion;
        if (target is not null && !ModrinthService.IsCompatible(target, gameVersion, loader))
            throw new InvalidOperationException($"La versión elegida de '{project.Name}' no es compatible con {gameVersion} y {loader}.");
        target ??= versions.FirstOrDefault(v => ModrinthService.IsCompatible(v, gameVersion, loader));
        if (target is null)
            throw new InvalidOperationException($"No hay una versión de '{project.Name}' compatible con {gameVersion} y {loader}.");
'''
assert old in s
s = s.replace(old, new, 1)

old = '''        var target = await _modrinth.GetVersionAsync(current.VersionId, cancellationToken);
        if (target is null)
            target = (await _modrinth.GetVersionsAsync(current.ProjectId, current.MinecraftVersion, current.Loader, cancellationToken))
                .FirstOrDefault(v => ModrinthService.IsCompatible(v, current.MinecraftVersion, current.Loader));
        if (target is null)
            throw new InvalidOperationException("No se encontró una versión de actualización compatible.");
'''
new = '''        var versions = await _modrinth.GetVersionsAsync(current.ProjectId, current.MinecraftVersion, current.Loader, cancellationToken);
        var target = versions
            .Where(v => !string.Equals(v.Id, current.VersionId, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(v => ModrinthService.IsCompatible(v, current.MinecraftVersion, current.Loader));
        target ??= await _modrinth.GetVersionAsync(current.VersionId, cancellationToken);
        if (target is null || string.Equals(target.Id, current.VersionId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No hay una versión más reciente compatible para actualizar.");
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
