# mk/dfios.sys.mk
#
# Global build knobs shared by every driver Makefile in this tree.
# Everything is built with BSD bmake(1); no GNU make idioms are used.
#
# Layout:
#
#	Builds/ios/		generated; safe to delete at any time (gitignored)
#	Builds/ios/proj/	Xcode project emitted by Unity (stage 1)
#	Builds/ios/app/		the .app built from it (stage 2)
#	Builds/ios/*.log	per-stage logs
#	Builds/ios/<bundle>.ipa	final signed archive (stage 3)
#
# The port is pinned to Unity 6 (6000.5.10f1) -- see ProjectSettings/ProjectVersion.txt.
# Unity 2022.3.62f3's macOS-arm64 iOS build support shipped a Linux-baked IL2CPP
# Bee backend (HostPlatform.IsOSX=false) that hard-fails iOS builds, so we moved
# to Unity 6, whose iOS backend reports the correct macOS host.  UNITY defaults
# to the Hub-managed install of the pinned editor.

TOP?=		${.CURDIR}

# --- paths -------------------------------------------------------------
BUILDDIR?=	${TOP}/Builds/ios
PROJDIR=	${BUILDDIR}/proj
APPDIR=		${BUILDDIR}/app
LOGDIR=		${BUILDDIR}

# Where Unity 6 lives (Unity Hub default install location).  Override
# on the command line if your editor is elsewhere:  bmake UNITY=/opt/Unity.App
UNITY_VERSION=	6000.5.10f1
UNITY_HUB=	/Applications/Unity/Hub/Editor
UNITY_DIR?=	${UNITY_HUB}/${UNITY_VERSION}
UNITY?=		${UNITY_DIR}/Unity.app/Contents/MacOS/Unity

# --- App identity / signing -------------------------------------------
# PROJECT_NAME is the Unity product name -- it becomes the .app bundle and
# the .ipa leaf.  This repo is a FORK, so the Unity-emitted Xcode project
# carries the upstream author's (empty) signing identity.  We overrule it.
#
#   SIGN_IDENTITY  codesign identity.  Default "-" = AD-HOC signing, which
#                  needs no provisioning profile and no registered Apple ID
#                  account.  That sidesteps the headless "No Account for
#                  Team" failure entirely and yields a .ipa that sideloads
#                  on a dev/trusted device.
#
#   For a proper store/device install later, set a real Development cert by
#   fingerprint and add the account to Xcode -> Settings -> Accounts:
#       bmake SIGN_IDENTITY=09B795DABD40E2B0A28930BA25AFE018FBD6CF96 \
#             DEVELOPMENT_TEAM=82PYZFRKJC
#
#   security find-identity -v -p codesigning
#       ) 09B795DABD40E2B0A28930BA25AFE018FBD6CF96
#         "Apple Development: sunnevanattsol@gmail.com (82PYZFRKJC)"
PRODUCT_NAME=	DaggerfallUnity
BUNDLE_ID=	net.codex64.daggerfall
SIGN_IDENTITY?=	-
DEVELOPMENT_TEAM=
SIGN_CERT=
CODESIGN?=	/usr/bin/codesign

# Test app variant (separate bundle id, own container).  1 = test, else release.
#	set DFU_IOS_TESTAPP=1
TESTAPP?=	${DFU_IOS_TESTAPP}

# Development build (debug transport + AllowDebugging).  1 = dev, else release.
#	set DFU_IOS_DEV=1
DEVBUILD?=	${DFU_IOS_DEV}

# --- output naming -----------------------------------------------------
.if ${TESTAPP:tl} == "1"
BUNDLE_ID=	net.codex64.daggerfall.test
PRODUCT_NAME=	DFUTest
.endif

# The .ipa leaf, spaces and all (they survive zip/archive paths fine).
IPA=		${BUILDDIR}/${BUNDLE_ID}.ipa
APP=		${APPDIR}/${PRODUCT_NAME}.app

# --- binaries ----------------------------------------------------------
XCODEBUILD?=	xcodebuild
UNZIP?=		/usr/bin/ditto

# Xcode developer directory for stage 2 (xcodebuild + Unity's il2cpp Bee).
# Unity 6 natively supports the current system Xcode 26 + iOS 26 SDK, so we use
# the system Xcode via xcode-select by default (empty).  For an older toolchain
# set it explicitly, e.g.  bmake XCODE_DEV_DIR=/Applications/Xcode-16.4.app/Contents/Developer
XCODE_DEV_DIR?=

ECHO=		echo
INSTALL_DIR=	mkdir -p

.PHONY: all clean
