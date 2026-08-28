# mk/ipa.mk
#
# Stage 3: package the ad-hoc-signed .app into an .ipa.
#
#	bmake -f mk/ipa.mk TOP=<repo> BUILDDIR=<build>
#
# Stage 2 built the .app, then ad-hoc signed it and copied it to ${APP}.
# An .ipa is simply a zip whose root holds a Payload/ directory containing
# the .app.  We use /usr/bin/ditto so resource forks and codesign metadata
# survive intact -- a plain `zip` can strip the embedded signature and refuse
# to install.

.include "${TOP}/mk/dfios.sys.mk"

PAYLOAD=	${BUILDDIR}/Payload

.PHONY: ipa

ipa:
	@if [ ! -d "${APP}" ]; then						\
		${ECHO} "error: signed .app not found at ${APP}.";		\
		${ECHO} "Run  bmake xcode  first.";				\
		exit 1;								\
	fi
	@${ECHO} "== stage 3: .app -> .ipa =="
	@rm -rf ${PAYLOAD}
	@mkdir -p ${PAYLOAD}
	@ditto "${APP}" "${PAYLOAD}/${PRODUCT_NAME}.app"
	@rm -f "${IPA}"
	@( cd ${BUILDDIR} && ditto -c -k --keepParent "./Payload" "${IPA}" )
	@rm -rf ${PAYLOAD}
	@${ECHO} "   ipa: ${IPA}"
	@${ECHO} "   bundle id: ${BUNDLE_ID}"
	@${ECHO} "   deploy:  xcrun devicectl device install app --device <id> '${IPA}'"
