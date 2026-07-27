#!/usr/bin/env node
/** Cross-platform helper: free a TCP port (best-effort, never fails the npm script). */
const { execSync } = require('child_process')

const port = String(process.argv[2] || '3000').replace(/\D/g, '') || '3000'

function run(cmd) {
  try {
    return execSync(cmd, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
  } catch {
    return ''
  }
}

try {
  if (process.platform === 'win32') {
    const out = run(`netstat -ano | findstr :${port}`)
    const pids = new Set()
    for (const line of out.split(/\r?\n/)) {
      if (!/LISTENING/i.test(line)) continue
      const parts = line.trim().split(/\s+/)
      const pid = parts[parts.length - 1]
      if (pid && /^\d+$/.test(pid) && pid !== '0') pids.add(pid)
    }
    for (const pid of pids) {
      try {
        execSync(`taskkill /F /PID ${pid}`, { stdio: 'ignore' })
      } catch {
        /* ignore */
      }
    }
  } else {
    try {
      execSync(`fuser -k ${port}/tcp`, { stdio: 'ignore' })
    } catch {
      /* ignore */
    }
  }
} catch {
  /* ignore */
}

process.exit(0)
