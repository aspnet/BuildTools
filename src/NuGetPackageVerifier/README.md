NuGetPackageVerifier
--------------------

Internal tool for verifying a nupkg meets certain requirements.

NuGetPackageVerifier.json schema
```js
{
    "$ruleSetName$": { /* if $ruleSetName$ == 'Default', the ruleset is run for packages not listed in any other ruleset */
        "rules": [ "$ruleNameToRun$" ],
        "packages": {
            "$packageId$": {
                "exclusions": {
                    "$ISSUE_ID$": {
                        "$file$": "$justification$"
                    }
                },
                "packageTypes": [ "$packageType$" ] /* Optional. For validating http://docs.nuget.org/create/package-types */
            }
        }
    }
}
```