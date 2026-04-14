# PAC Packaging

Use this reference when reasoning about durable Dataverse release artifacts rather than direct live mutation.

## Key PAC Commands

- export a solution:
  - `pac solution export --environment <url> --name <solution> --path <zip> --overwrite`
- unpack a solution ZIP:
  - `pac solution unpack --zipfile <zip> --folder <dir> --packagetype Unmanaged --allowWrite --allowDelete --clobber`
- pack source into a ZIP:
  - `pac solution pack --folder <dir> --zipfile <zip> --packagetype Unmanaged|Managed --allowWrite --allowDelete`
- publish after live mutation:
  - `pac solution publish --environment <url>`
- register an existing component to an unmanaged solution:
  - `pac solution add-solution-component --environment <url> --solutionUniqueName <solution> --component <id-or-schema> --componentType <type>`
- generate deployment settings:
  - `pac solution create-settings --solution-zip <managed-zip> --settings-file <json>`
- run solution check:
  - `pac solution check --path <zip> --outputDirectory <dir> --geo <region>`

## XML Versus YAML Layouts

Two source families matter:
- XML unpacked source
  - `Other/Solution.xml`
  - `Other/Customizations.xml`
  - `Entities/...`
  - best when you need close inspection of FormXML and emitted metadata
- YAML source-control layout
  - `solutions/`
  - PAC-first source representation
  - useful for more structured source-control scenarios

The helper scripts bundled with this skill deeply parse XML exports and XML-unpacked folders. They do not deeply parse YAML source-control repositories; use the skill references and PAC guidance for those.

## Durable Release Outputs

Tracked solution source should be able to produce:
- unmanaged ZIPs for `Dev`
- managed ZIPs for governed higher environments
- settings templates
- optional solution-check reports

If a workflow cannot regenerate those outputs from tracked source, it is not yet a durable release path.

## Recommended Pattern

1. Author from the canonical app model.
2. Use direct Dev apply only when it helps prove the model quickly.
3. Export or read back the result.
4. Emit tracked solution source.
5. Pack with PAC.
6. Promote packaged artifacts through the governed environment path.

## Important Warning

Do not confuse:
- unpacked XML used for inspection and packaging

with:
- the primary product authoring model

The solution package is an artifact family. It is essential, but it should usually be emitted from a higher-level model or from deliberate tracked source maintenance, not hand-edited as the first design surface.
