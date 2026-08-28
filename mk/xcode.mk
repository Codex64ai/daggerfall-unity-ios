# mk/xcode.mk
#
# Stage 2: build the .app from the Xcode project Unity emitted, then
# ad-hoc sign it.
#
#	bmake -f mk/xcode.mk TOP=<repo> BUILDDIR=<build>
#
# Unity names its generated project Unity-iPhone.xcodeproj and the scheme
# Unity-iPhone.  xcodebuild compiles to a derived-data dir under BUILDDIR.
#
# Signing strategy:
#   * Headless xcodebuild automatic provisioning fails on this box with
#     "No Account for Team ..." -- there is no registered Apple ID account to
#     fetch a profile for.  So we build with codesigning DISABLED (compiles
#     cleanly, no account needed), then ad-hoc sign the .app with
#     `codesign -s -`.  Ad-hoc signing needs no team, no profile, no account,
#     and yields a .ipa that sideloads on a trusted device.
#   * To use a real Development identity later, set SIGN_IDENTITY and
#     DEVELOPMENT_TEAM (see dfios.sys.mk) and add the account to
#     Xcode -> Settings -> Accounts.
#
# Toolchain / SDK:
#   Unity 6 natively supports the current system Xcode 26 + iOS 26 SDK, so by
#   default (XCODE_DEV_DIR empty) both xcodebuild AND the il2cpp Bee subprocess
#   it spawns use the system Xcode via xcode-select.  Set XCODE_DEV_DIR only to
#   force an older toolchain.

.include "${TOP}/mk/dfios.sys.mk"

XCPROJECT=	${PROJDIR}/Unity-iPhone.xcodeproj
XCDERIVED=	${BUILDDIR}/derived
XSCHEME=	Unity-iPhone

# The .app lands in the derived data build-products dir for the generic iOS
# destination.  xcodebuild locates it deterministically.
APP_SOURCE=	${XCDERIVED}/Build/Products/Release-iphoneos/${PRODUCT_NAME}.app

.PHONY: xcode

xcode:
	@if [ ! -d "${XCPROJECT}" ]; then					\
		${ECHO} "error: ${XCPROJECT} not found.";			\
		${ECHO} "Run  bmake unity  first so Unity emits the project.";	\
		exit 1;								\
	fi
	@if [ -n "${XCODE_DEV_DIR}" ] && [ ! -d "${XCODE_DEV_DIR}" ]; then	\
		${ECHO} "error: XCODE_DEV_DIR=${XCODE_DEV_DIR} not found.";	\
		exit 1;								\
	fi
	@${ECHO} "== stage 2: xcodebuild -> .app (unsigned) =="
	@if [ -n "${XCODE_DEV_DIR}" ]; then					\
		${ECHO} "   developer dir for this build: ${XCODE_DEV_DIR}";	\
	fi
	@mkdir -p ${XCDERIVED}
	@if [ -n "${XCODE_DEV_DIR}" ]; then					\
		env DEVELOPER_DIR="${XCODE_DEV_DIR}" ${XCODEBUILD} \
		   -project "${XCPROJECT}" \
		   -scheme "${XSCHEME}" \
		   -configuration Release \
		   -destination 'generic/platform=iOS' \
		   -derivedDataPath "${XCDERIVED}" \
		   CODE_SIGNING_ALLOWED=NO \
		   ONLY_ACTIVE_ARCH=NO \
		   ${XCODEBUILD_EXTRA_FLAGS} \
		   > "${LOGDIR}/xcodebuild.log" 2>&1; \
	else							\
		${XCODEBUILD} \
		   -project "${XCPROJECT}" \
		   -scheme "${XSCHEME}" \
		   -configuration Release \
		   -destination 'generic/platform=iOS' \
		   -derivedDataPath "${XCDERIVED}" \
		   CODE_SIGNING_ALLOWED=NO \
		   ONLY_ACTIVE_ARCH=NO \
		   ${XCODEBUILD_EXTRA_FLAGS} \
		   > "${LOGDIR}/xcodebuild.log" 2>&1; \
	fi
	@if ! grep -q "BUILD SUCCEEDED" "${LOGDIR}/xcodebuild.log" 2>/dev/null; then	\
		${ECHO} "   xcodebuild FAILED; see ${LOGDIR}/xcodebuild.log";	\
		exit 1;								\
	fi
	@${ECHO} "   xcodebuild ok"
	@if [ ! -d "${APP_SOURCE}" ]; then					\
		${ECHO} "error: built .app not found at ${APP_SOURCE}.";	\
		exit 1;								\
	fi
	@${ECHO} "== stage 2b: ad-hoc sign .app =="
	@rm -rf "${APPDIR}"
	@mkdir -p "${APPDIR}"
	@ditto "${APP_SOURCE}" "${APP}"
	@${CODESIGN} --force --deep -s "${SIGN_IDENTITY}" "${APP}" \
		&& ${ECHO} "   signed: ${APP}" || { ${ECHO} "   codesign FAILED"; exit 1; }
	@${ECHO} "   identity: ${SIGN_IDENTITY} (ad-hoc)"
	@${ECHO} "   verify:  ${CODESIGN} --verify --deep --strict ${APP}"
	@${CODESIGN} --verify --deep --strict "${APP}" && ${ECHO} "   verify: OK" || true
