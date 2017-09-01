FROM microsoft/aspnet:4.6.2


# DevPack returns exit 0 immediately, but it's not done, so we wait.
# A more correct thing would be to block on a registry key existing or similar.
RUN \
        Invoke-WebRequest https://download.microsoft.com/download/F/1/D/F1DEB8DB-D277-4EF9-9F48-3A65D4D8F965/NDP461-DevPack-KB3105179-ENU.exe -OutFile ~\\net461dev.exe ; \
        ~\\net461dev.exe /Passive /NoRestart ; \
        Start-Sleep -s 10; \
        Remove-Item ~\\net461dev.exe -Force ;

WORKDIR c:\\repo

ADD . .

ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

ENTRYPOINT ["build.cmd"]
