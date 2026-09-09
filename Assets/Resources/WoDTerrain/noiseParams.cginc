// MOBILE PORT - github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40, Assets/Shaders/noiseParams.cginc; compiled into the app (Resources.Load), no licence header upstream, private draft only.
int perlinTile_octaves;
float perlinTile_frequency, perlinTile_amplitude, perlinTile_lacunarity, perlinTile_persistence, perlinTile_maxHeight;
float2 perlinTile_offset;
PerlinParams PerlinTile(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = perlinTile_octaves;
    p.frequency = perlinTile_frequency;
    p.amplitude = perlinTile_amplitude;
    p.lacunarity = perlinTile_lacunarity;
    p.persistence = perlinTile_persistence;
    p.offset = perlinTile_offset;
    p.maxHeight = perlinTile_maxHeight;

    return p;
}

int perlinBump_octaves;
float perlinBump_frequency, perlinBump_amplitude, perlinBump_lacunarity, perlinBump_persistence, perlinBump_maxHeight;
float2 perlinBump_offset;
PerlinParams PerlinBump(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = perlinBump_octaves;
    p.frequency = perlinBump_frequency;
    p.amplitude = perlinBump_amplitude;
    p.lacunarity = perlinBump_lacunarity;
    p.persistence = perlinBump_persistence;
    p.offset = perlinBump_offset;
    p.maxHeight = perlinBump_maxHeight;

    return p;
}

int iqMountain_octaves;
float iqMountain_frequency, iqMountain_amplitude, iqMountain_lacunarity, iqMountain_persistence, iqMountain_maxHeight, iqMountain_warp, iqMountain_ridgeOffset;
float2 iqMountain_offset;
SwissParams IQMountain(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = iqMountain_octaves;
    p.frequency = iqMountain_frequency;
    p.amplitude = iqMountain_amplitude;
    p.lacunarity = iqMountain_lacunarity;
    p.persistence = iqMountain_persistence;
    p.offset = iqMountain_offset;
    p.maxHeight = iqMountain_maxHeight;
    p.warp = iqMountain_warp;
    p.ridgeOffset = iqMountain_ridgeOffset;

    return p;
}

int swissFolded_octaves;
float swissFolded_frequency, swissFolded_amplitude, swissFolded_lacunarity, swissFolded_persistence, swissFolded_ridgeOffset, swissFolded_warp, swissFolded_maxHeight;
float2 swissFolded_offset;
SwissParams SwissFolded(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = swissFolded_octaves;
    p.frequency = swissFolded_frequency;
    p.amplitude = swissFolded_amplitude;
    p.lacunarity = swissFolded_lacunarity;
    p.persistence = swissFolded_persistence;
    p.offset = swissFolded_offset;
    p.ridgeOffset = swissFolded_ridgeOffset;
    p.warp = swissFolded_warp;
    p.maxHeight = swissFolded_maxHeight;

    return p;
}

int mountainBase_octaves;
float mountainBase_frequency, mountainBase_amplitude, mountainBase_lacunarity, mountainBase_persistence, mountainBase_ridgeOffset, mountainBase_warp, mountainBase_maxHeight;
float2 mountainBase_offset;
SwissParams MountainBase(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = mountainBase_octaves;
    p.frequency = mountainBase_frequency;
    p.amplitude = mountainBase_amplitude;
    p.lacunarity = mountainBase_lacunarity;
    p.persistence = mountainBase_persistence;
    p.offset = mountainBase_offset;
    p.ridgeOffset = mountainBase_ridgeOffset;
    p.warp = mountainBase_warp;
    p.maxHeight = mountainBase_maxHeight;

    return p;
}

int hillBase_octaves;
float hillBase_frequency, hillBase_amplitude, hillBase_lacunarity, hillBase_persistence, hillBase_maxHeight;
float2 hillBase_offset;
PerlinParams HillBase(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = hillBase_octaves;
    p.frequency = hillBase_frequency;
    p.amplitude = hillBase_amplitude;
    p.lacunarity = hillBase_lacunarity;
    p.persistence = hillBase_persistence;
    p.offset = hillBase_offset;
    p.maxHeight = hillBase_maxHeight;

    return p;
}

int swissFaults_octaves;
float swissFaults_frequency, swissFaults_amplitude, swissFaults_lacunarity, swissFaults_persistence, swissFaults_ridgeOffset, swissFaults_warp, swissFaults_maxHeight;
float2 swissFaults_offset;
SwissParams SwissFaults(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = swissFaults_octaves;
    p.frequency = swissFaults_frequency;
    p.amplitude = swissFaults_amplitude;
    p.lacunarity = swissFaults_lacunarity;
    p.persistence = swissFaults_persistence;
    p.offset = swissFaults_offset;
    p.ridgeOffset = swissFaults_ridgeOffset;
    p.warp = swissFaults_warp;
    p.maxHeight = swissFaults_maxHeight;

    return p;
}

int swissCell_octaves;
float swissCell_frequency, swissCell_amplitude, swissCell_lacunarity, swissCell_persistence, swissCell_ridgeOffset, swissCell_warp, swissCell_maxHeight;
float2 swissCell_offset;
SwissParams SwissCell(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = swissCell_octaves;
    p.frequency = swissCell_frequency;
    p.amplitude = swissCell_amplitude;
    p.lacunarity = swissCell_lacunarity;
    p.persistence = swissCell_persistence;
    p.offset = swissCell_offset;
    p.ridgeOffset = swissCell_ridgeOffset;
    p.warp = swissCell_warp;
    p.maxHeight = swissCell_maxHeight;

    return p;
}

int jordanFolded_octaves;
float2 jordanFolded_offset;
float jordanFolded_frequency, jordanFolded_amplitude, jordanFolded_lacunarity, jordanFolded_persistence, jordanFolded_persistence1, jordanFolded_warp0, jordanFolded_warp, jordanFolded_damp, jordanFolded_damp0, jordanFolded_damp_scale, jordanFolded_maxHeight;
JordanParams JordanFolded(float2 pos) {
    JordanParams p;

    p.pos = pos;
    p.octaves = jordanFolded_octaves;
    p.frequency = jordanFolded_frequency;
    p.amplitude = jordanFolded_amplitude;
    p.lacunarity = jordanFolded_lacunarity;
    p.persistence = jordanFolded_persistence;
    p.persistence1 = jordanFolded_persistence1;
    p.offset = jordanFolded_offset;
    p.warp0 = jordanFolded_warp0;
    p.warp = jordanFolded_warp;
    p.damp = jordanFolded_damp;
    p.damp0 = jordanFolded_damp0;
    p.damp_scale = jordanFolded_damp_scale;
    p.maxHeight = jordanFolded_maxHeight;

    return p;
}

int perlinDune_octaves;
float perlinDune_frequency, perlinDune_amplitude, perlinDune_lacunarity, perlinDune_persistence, perlinDune_maxHeight;
float2 perlinDune_offset;
PerlinParams PerlinDune(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = perlinDune_octaves;
    p.frequency = perlinDune_frequency;
    p.amplitude = perlinDune_amplitude;
    p.lacunarity = perlinDune_lacunarity;
    p.persistence = perlinDune_persistence;
    p.offset = perlinDune_offset;
    p.maxHeight = perlinDune_maxHeight;

    return p;
}

int swissDune_octaves;
float swissDune_frequency, swissDune_amplitude, swissDune_lacunarity, swissDune_persistence, swissDune_ridgeOffset, swissDune_warp, swissDune_maxHeight;
float2 swissDune_offset;
SwissParams SwissDune(float2 pos) {
    SwissParams p;

    p.pos = pos;
    p.octaves = swissDune_octaves;
    p.frequency = swissDune_frequency;
    p.amplitude = swissDune_amplitude;
    p.lacunarity = swissDune_lacunarity;
    p.persistence = swissDune_persistence;
    p.offset = swissDune_offset;
    p.ridgeOffset = swissDune_ridgeOffset;
    p.warp = swissDune_warp;
    p.maxHeight = swissDune_maxHeight;

    return p;
}

int mntVar_octaves;
float mntVar_frequency, mntVar_amplitude, mntVar_lacunarity, mntVar_persistence, mntVar_maxHeight;
float2 mntVar_offset;
PerlinParams MountainVar(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = mntVar_octaves;
    p.frequency = mntVar_frequency;
    p.amplitude = mntVar_amplitude;
    p.lacunarity = mntVar_lacunarity;
    p.persistence = mntVar_persistence;
    p.offset = mntVar_offset;
    p.maxHeight = mntVar_maxHeight;

    return p;
}

int colorVar_octaves;
float colorVar_frequency, colorVar_amplitude, colorVar_lacunarity, colorVar_persistence, colorVar_maxHeight;
float2 colorVar_offset;
PerlinParams ColorVar(float2 pos) {
    PerlinParams p;

    p.pos = pos;
    p.octaves = colorVar_octaves;
    p.frequency = colorVar_frequency;
    p.amplitude = colorVar_amplitude;
    p.lacunarity = colorVar_lacunarity;
    p.persistence = colorVar_persistence;
    p.offset = colorVar_offset;
    p.maxHeight = colorVar_maxHeight;

    return p;
}