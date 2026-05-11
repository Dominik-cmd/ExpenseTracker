# Contributing to ExpenseTracker

First off, thank you for considering contributing to ExpenseTracker! It's people like you that make ExpenseTracker such a great tool.

## Getting Started

1. **Fork the repository** and clone your fork locally.
2. **Backend (.NET)**:
   - Make sure you have the .NET SDK installed.
   - You can open the solution `ExpenseTracker.slnx` in Visual Studio, Rider, or VS Code.
   - Run `docker-compose up -d` to spin up the required database (PostgreSQL/SQL Server depending on config).
   - Run the API project (`ExpenseTracker.Api`).
3. **Frontend (Angular)**:
   - Make sure you have Node.js and npm installed.
   - Navigate to `expensetracker.client/` and run `npm install`.
   - Run `npm start` to serve the application locally.

## Development Workflow

1. Create a branch for your feature or bugfix:
   - `feature/your-feature-name`
   - `bugfix/issue-description`
   - `docs/documentation-update`
2. Make your changes and commit them:
   - `feat: add new dashboard widget`
   - `fix: resolve issue with saving transactions`
   - `docs: update readme instructions`
3. Push your branch to your fork and submit a Pull Request against the `main` branch.

## Code Quality Standards

We enforce code quality through automated GitHub Actions. Before submitting a PR, please ensure:

### .NET Backend
- We use standard Microsoft C# coding conventions.
- Format your code: Run `dotnet format ExpenseTracker.slnx` to automatically fix formatting.
- Ensure tests pass: Run `dotnet test ExpenseTracker.slnx`.

### Angular Frontend
- We use strict TypeScript and Angular ESLint rules.
- Check formatting/linting: Run `npm run lint` inside the `expensetracker.client` directory.
- Run `npm run lint -- --fix` to fix common issues automatically.

## Submitting a Pull Request

- Fill out the provided PR template.
- Make sure to link any related issues (e.g., `Fixes #123`).
- Ensure all CI checks pass. If they fail, review the logs, fix the issues locally, and push the updates.
