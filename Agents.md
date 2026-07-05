# Agents

# Code Generation Rules
- Always enforce `.editorconfig` rules.
- Always build the website by running `build-website.bat` from the repository root; do not call `dotnet build` directly for website builds.
- Do not test generated websites yourself; the user will check them.
- When editing markdown files with box-drawing characters (for example the solution tree), preserve the original encoding and characters exactly to avoid mojibake.
- Follow standard .NET naming conventions (naming violations are treated as errors).
- Never use NuGet packages that are not MIT or Apache 2 unless specifically instructed to.
- Always use braces, even if it is not technically needed (example: Add braces for an if statement with a single line of code inside it).
- When initializing collections, prefer `[]` instead of `new List<>()` or `Array.Empty<>` (example: `[]` instead of `Array.Empty<Car>()` and `new List<Car>()`).
- Never use the `var` keyword (always use explicit types).
- Always use primary constructors over regular constructors.
- Always use simplified `new()` over explicit construction when the type is apparent (example: `Car c = new();` instead of `Car c = new Car();`).
- When the created type is not evident, use an explicit type in `new` expressions.
- Convert properties to auto-properties when a manual backing field is not needed.
- Avoid duplicated sequential `if` branches; merge or refactor them into a single branch.
- Merge compatible null/value/pattern checks into logical patterns using `or` / `and` when possible.
- Do not require `this.` qualification for fields, properties, methods, or events.
- Always make namespaces match the folder structure.
- Keep single-line statements and single-line blocks on one line when possible.
- Private fields should be prefixed with `_` to limit the need for this keyword.
- Don't add XML summaries and trivial comments (this is an internal project).
- Always place private classes at the bottom of parent classes.
- Always make methods that have the async keyword use the `Async` suffix (example: correct `async Task SaveAsync`, wrong `async Task Save`).
