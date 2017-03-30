#!groovy

def PowerShell(psCmd) {
    bat "powershell.exe -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"$psCmd; EXIT \$global:LastExitCode\""
}

stage("tests") {
    parallel (
        windows: {
            node('nugetci-vsts-02') {
                ws("w\\${env.BRANCH_NAME.replaceAll('/', '-')}") {
                    checkout scm
                    PowerShell(". '.\\configure.ps1' -ci -v")

                    bat "\"${tool 'MSBuild'}\" build\\build.proj /t:RestoreVS15 /p:Configuration=Release /p:ReleaseLabel=xprivate /p:BuildNumber=9999 /v:m /m:1"

                    try {
                        bat "\"${tool 'MSBuild'}\" build\\build.proj /t:RunVS15 /p:Configuration=Release /p:ReleaseLabel=xprivate /p:BuildNumber=9999 /v:m /m:1"
                    }
                    finally {
                        junit 'artifacts/TestResults/*.xml'
                    }
                }
            }
        },
        linux: {
            node('master') {
                try {
                    sh './build.sh'
                }
                finally {
                    junit 'artifacts/TestResults/*.xml'
                }
            }
        },
        failFast: false
    )
}