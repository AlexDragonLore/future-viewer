# Private birthday invitation

Route: `/birthday#key=<secret>`. The fragment is removed before the key is
exchanged for a seven-day encrypted ticket. There is no public menu link.
The page disables analytics and indexing; all game and asset responses use
`Cache-Control: no-store`. No account is required.

The server checks every answer. Each of the five stages awards 100 points;
wrong answers provide hints without penalties. Only a completed ticket can
retrieve the postcard and original 15,000-ruble Ozon certificate. Replaying
the game or downloading the JPEG does not redeem the certificate.

Configure these **server-only** values:

| Setting / environment variable | Value |
| --- | --- |
| `Birthday__KeyHash` | SHA-256 hex digest of a randomly generated secret |
| `Birthday__CertificatePath` | `/app/private-birthday/certificate.jpg` |
| `Birthday__PostcardPath` | `/app/private-birthday/postcard.jpg` |

Keep the original JPEGs in `/opt/fv-app/birthday`, readable by the backend
container user (UID 1654), with no public web-server mapping. Include
`-f docker-compose.prod.birthday.yml` after the existing production overrides
when recreating the backend. Preserve the existing data-protection volume.
Missing configuration/files disables the invitation. Rotating the key hash
also invalidates tickets issued with the old key.

For local QA, use private files outside `frontend/public` and absolute paths
in the same settings. The optional real-API Playwright suite
`tests/e2e/birthday.e2e.ts` expects a backend at `127.0.0.1:5036` and
`FV_BIRTHDAY_QA_KEY` in the test process environment. Never put the real key
in source, a `VITE_*` variable, an HTTP query string or test snapshots.
