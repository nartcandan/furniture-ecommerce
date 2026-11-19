import { execSync } from 'node:child_process'

/** Removes the test database once the run is over. Its data lives in memory, so nothing is left behind. */
export default function globalTeardown() {
  execSync('docker compose -f ../docker-compose.e2e.yml down', { stdio: 'ignore' })
}
