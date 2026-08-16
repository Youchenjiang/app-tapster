# Security Policy

## Reporting Security Issues

We take the security of **Tapster** seriously. If you discover or suspect a security vulnerability, please report it responsibly. **Do not create public GitHub issues for security vulnerabilities.**

### How to Report

1. **GitHub Private Vulnerability Reporting (Preferred)**:
   - Navigate to the [Security Advisories tab](https://github.com/Youchenjiang/Tapster/security/advisories) on GitHub.
   - Click **"Report a vulnerability"** to submit your findings privately.

2. **Security Contact**:
   - Alternatively, contact the maintainer directly via GitHub profile contact options.

Please provide:
- A clear description of the vulnerability and affected components.
- Step-by-step reproduction instructions or a minimal Proof-of-Concept (PoC).
- Potential impact assessment.

---

## Response SLA and Remediation Process

- **Initial Acknowledgment**: Within **48 hours**.
- **Assessment and Triage**: Within **5 business days**.
- **Fix Delivery & Coordinated Disclosure**: Typically within **14 to 30 days**, coordinated with the reporter.

---

## Supported Versions

Only the latest active release branch and main development trunk receive security updates:

| Version | Supported          | Security Maintenance Status |
| ------- | ------------------ | --------------------------- |
| 1.1.x   | :white_check_mark: | Active Security Maintenance |
| 1.0.x   | :white_check_mark: | Critical Fixes Only         |
| < 1.0   | :x:                | End of Life (Unsupported)   |

---

## Security Best Practices & Privacy Guarantee

- **100% Offline & Local Execution**: Tapster operates entirely on local Win32 APIs with **zero telemetry and zero network communication**.
- **Input Safety**: Tapster does not log, export, or transmit keystrokes or mouse actions to any remote servers.
- **Supply Chain Integrity**: All releases are built using pinned dependencies, automated SAST scanning (CodeQL), and OpenSSF Scorecard security audits.
