# Security

## Reporting a problem

Please don't open a public issue for a security problem. Use GitHub's
**Security → Report a vulnerability** (private vulnerability reporting) on this repository instead,
with steps to reproduce. I'll reply within a week.

## Scope notes

- The demo accounts (`test123`, `admin123`) exist only when `SEED_DEMO_USERS=true` (the Debug default) and
  are not a vulnerability. Release builds turn the setting off.
- Rate limits are in-memory per process by design (see README → Rate limiting).

The security design is described in the README's **Security** section.
