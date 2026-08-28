# mk/unity.mk
#
# Stage 1: run the editor headlessly to produce the Xcode project.
#
#	bmake -f mk/unity.mk TOP=<repo> BUILDDIR=<build>
#
# Unity's iOS pipeline emits an Xcode *project* (not an .ipa); the heavy lifting
# happens inside Assets/Editor/MobileBuildSetup.cs (BuildIOS), which:
#
#	1. Applies iOS player settings (IL2CPP, Metal, landscape, bundle id...)
#	2. Builds Addressables content (localization string tables)
#	3. Runs BuildPipeline.BuildPlayer for BuildTarget.iOS into DFU_IOS_BUILD_PATH
#
# The same file's OnPostprocessBuild (Assets/Editor/MobileIOSPostProcess.cs) then
# patches Info.plist and the pbxproj for Xcode 16+. Everything Unity emits is
# considered generated and lives under BUILDDIR.
#
# The editor must be Unity 6 (6000.5.10f1) -- see mk/dfios.sys.mk.  If it is
# missing the `unity-install` target explains exactly which modules to install;
# `unity` itself fails fast rather than silently building with the wrong editor.

.include "${TOP}/mk/dfios.sys.mk"

# ---- editor discovery --------------------------------------------------

.PHONY: unity unity-install check _unity-check-editor

# `unity` fails fast when the expected editor is absent, so a wrong machine or
# a stale UNITY= override is caught here instead of mid-IL2CPP.
unity: _unity-check-editor
	@${ECHO} "== stage 1: Unity ${UNITY_VERSION} -> Xcode project =="
	@${ECHO} "   editor: ${UNITY}"
	@mkdir -p ${PROJDIR}
	@DFU_IOS_BUILD_PATH="${PROJDIR}" \
	 DFU_IOS_TESTAPP="${TESTAPP}" \
	 DFU_IOS_DEV="${DEVBUILD}" \
	 "${UNITY}" -batchmode -nographics -quit \
	   -projectPath "${TOP}" \
	   -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.BuildIOS \
	   -logFile "${LOGDIR}/unity.log"
	@${ECHO} "   Unity finished; project at ${PROJDIR}"

_unity-check-editor:
	@if [ ! -x "${UNITY}" ]; then						\
		${ECHO} "error: Unity ${UNITY_VERSION} editor not found at:";	\
		${ECHO} "   ${UNITY}";						\
		${ECHO} "Run  bmake unity-install  for the required modules, or";	\
		${ECHO} "override with  bmake UNITY=/path/to/Unity";		\
		exit 1;								\
	fi

# ---- install guidance --------------------------------------------------

unity-install:
	@${ECHO} "== Unity ${UNITY_VERSION} install -- the port is pinned to this line =="
	@${ECHO}
	@${ECHO} "This needs a multi-GB editor download plus the iOS modules, from"
	@${ECHO} "Unity's download archive (not the App Store)."
	@${ECHO}
	@${ECHO} "Easiest, via Unity Hub:"
	@${ECHO} "   1. Unity Hub -> Installs -> Install Editor -> Archive tab"
	@${ECHO} "   2. pick  6000.5.10f1"
	@${ECHO} "   3. in Modules, check:  iOS Build Support (IL2CPP)"
	@${ECHO} "                         iOS Build Support (Mac)"
	@${ECHO}
	@${ECHO} "That lands the editor at:"
	@${ECHO} "   ${UNITY_DIR}"
	@${ECHO}
	@${ECHO} "Once present,  bmake unity  should find it.  To skip the archive"
	@${ECHO} "search, give bmake the direct path:  bmake UNITY=/path/to/Unity"

# ---- self test ---------------------------------------------------------

check: _unity-check-editor
	@${ECHO} "== headless self test =="
	@DFU_IOS_BUILD_PATH="${PROJDIR}" \
	 "${UNITY}" -batchmode -nographics -quit \
	   -projectPath "${TOP}" \
	   -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll \
	   -logFile "${LOGDIR}/selftest.log"
	@${ECHO} "   self test exit: $$? (0 = all passed)"
