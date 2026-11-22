# Repository guidelines

## C# and public contracts

- Inspect nearby implementations and preserve existing conventions. Keep changes focused on the requested behavior.
- Put each top-level class, interface, record, struct and enum in its own file, named after the type. Private nested implementation types may stay at the bottom of their containing type.
- Use four-space indentation, UTF-8, CRLF line endings and a final newline. Remove trailing whitespace and unused imports.
- Put control-flow bodies on separate, indented lines. Never put `return`, `continue` or `break` on the same line as its condition. Use braces for multiline bodies and all branches of an `if` with `else`/`else if`.
- Simple method/constructor argument guards may remain on one line; prefer `ArgumentNullException.ThrowIfNull` and similar helpers.
- Separate logical steps with blank lines. Expand long calls with one argument per line; keep short calls compact. Expand enum members and empty constructors onto separate lines.
- Every C# type must belong to a namespace. Prefer file-scoped namespaces. Use `var` for built-in or apparent types; use explicit types when a method result does not make the type apparent. Follow `.editorconfig` for target-typed construction and formatting.
- Keep the public surface small. Document public contracts with useful XML comments matching the existing package, and preserve compatibility unless a breaking change is explicitly agreed.
- Do not rename existing public members as part of formatting or file organization.

## Examples, tests and local material

- Keep runnable examples and their mapping/converter implementations centralized in `samples/Relewise.DemoStorefront`. Link guides to those source files rather than adding separate example projects or duplicate implementations. Preserve culture-aware mapping, nested block extraction and legacy compatibility examples when updating the demo.
- Keep credentials, generated Umbraco databases, ad hoc smoke-test tooling and raw execution reports in ignored local files. Do not publish them with the package.
- Keep permanent backend tests focused on observable behavior, regressions and compatibility. The DemoStorefront deliberately has no frontend test suite; verify its UI in a browser. Build affected projects and run relevant backend tests after code changes.
- Build the dashboard with `npm run build` in `src/Integrations.Umbraco/Client` when changing frontend code.
