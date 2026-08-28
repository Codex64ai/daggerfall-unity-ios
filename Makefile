# Daggerfall Unity -- iOS touch port -- top-level Makefile (BSD bmake).
#
#	export PATH=/opt/xnuports/bin:$PATH;  bmake
#
# Targets:
#	all		build the signed .ipa (unity -> xcode -> ipa) into Builds/ios/
#	unity		run Unity headless: Addressables + IL2CPP BuildPlayer -> Xcode project
#	xcode		build the .app from the generated Xcode project
#	ipa		package and code-sign the .app into a deployable .ipa
#	check		run the touch-layer self test headlessly (fail-fast gate)
#	unity-install	print what 2022.3.62f3 modules are needed and how to install them
#	clean		remove Builds/ entirely
#
# The iOS pipeline is three stages, each driven from mk/ so the driver Makefile
# here stays a stub -- the same shape as the xnuports trees (see README for the
# layout, mk/*.mk for the build).
#
#	stage 1  mk/unity.mk   -- Unity BuildPlayer emits an Xcode *project*
#	stage 2  mk/xcode.mk   -- xcodebuild turns that project into a .app
#	stage 3  mk/ipa.mk     -- sign + zip the .app into an .ipa
#
# Project pin: 2022.3.62f3 (ProjectSettings/ProjectVersion.txt). Unity 6 is NOT
# the target -- the README and the Xcode-16 post-process were validated against
# 2022.3, and iOS IL2CPP is version-sensitive.

TOP?=		${.CURDIR}

.include "${TOP}/mk/dfios.sys.mk"

# Stage drivers pull the per-stage Makefile in with TOP carried through so
# child invocations resolve mk/*.mk identically.
UNITY_MAKE=	bmake -f ${TOP}/mk/unity.mk TOP=${TOP} BUILDDIR=${BUILDDIR}
XCODE_MAKE=	bmake -f ${TOP}/mk/xcode.mk TOP=${TOP} BUILDDIR=${BUILDDIR}
IPA_MAKE=	bmake -f ${TOP}/mk/ipa.mk TOP=${TOP} BUILDDIR=${BUILDDIR}

all: ipa
	@${ECHO} "== dfu-ios build complete =="
	@${ECHO} "   ipa: ${IPA}"

ipa: xcode
	${IPA_MAKE} ipa

xcode: unity
	${XCODE_MAKE} xcode

unity:
	${UNITY_MAKE} unity

check:
	${UNITY_MAKE} check

unity-install:
	${UNITY_MAKE} unity-install

clean:
	rm -rf ${BUILDDIR}

.PHONY: all ipa xcode unity check unity-install clean
