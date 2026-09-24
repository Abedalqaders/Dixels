import { useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../components/Sidebar'
import { getDisplayName } from '../auth/roles'
import '../styles/tokens.css'
import '../styles/base.css'
import '../styles/dashboard.css'

// Static sample data for now - no backend yet, this is the visual design
// only. Shapes loosely mirror what the real BookingsAppService will return.
const UPCOMING = [
  {
    group: 'Today',
    items: [
      { day: '21', dow: 'MON', time: '09:00 – 10:00', title: 'Product sync', meta: 'Meeting Room 3B · Level 3' },
      { day: '21', dow: 'MON', time: '14:00 – 15:30', title: 'Focus block', meta: 'Focus Pod 2-04 · Level 2' },
    ],
  },
  {
    group: 'Wed 23 Sep',
    items: [
      { day: '23', dow: 'WED', time: '10:00 – 11:00', title: 'Design review', meta: 'Meeting Room 3C · Level 3' },
      { day: '23', dow: 'WED', time: '15:00 – 16:00', title: '1:1', meta: 'Focus Pod 2-04 · Level 2' },
    ],
  },
]

const DAY_OPEN_START = 7
const DAY_OPEN_END = 20
const DAY_BOOKINGS = [{ start: 9, end: 10, label: 'Meeting Room 3B' }]

export function DashboardPage() {
  const auth = useAuth()
  const [qbDuration, setQbDuration] = useState(1)
  const [cancelling, setCancelling] = useState<string | null>(null)

  const firstName = getDisplayName(auth.user).split(/[\s._-]+/)[0]

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="top">
          <span className="pick"><span className="picklbl">Ridge House</span></span>
        </div>

        <div className="content">
          <div className="dashhead">
            <div>
              <h1 className="pagetitle">Good morning, {firstName}</h1>
              <p className="lead">
                Monday 21 September · Ridge House is open 07:00 – 20:00 today · you can book 14 days ahead.
              </p>
            </div>
          </div>

          <div className="stats">
            <div className="stat">
              <span className="lbl2">Today</span>
              <span className="num">2</span>
              <span className="sub">Next at 09:00 · Meeting Room 3B</span>
            </div>
            <div className="stat">
              <span className="lbl2">This week</span>
              <span className="num">4</span>
              <span className="sub">Across 3 days, through Wed 23 Sep</span>
            </div>
            <div className="stat">
              <span className="lbl2">Hours booked</span>
              <span className="num">15h</span>
              <span className="sub">This week, across your bookings</span>
            </div>
          </div>

          <div className="dashgrid">
            <div className="dashcol">
              <section className="card upnextcard">
                <div className="upnext">
                  <div className="upnext-time">
                    <span className="from">09:00</span>
                    <span className="to">until 10:00</span>
                  </div>
                  <div className="upnext-body">
                    <span className="upnext-eyebrow">Up next</span>
                    <span className="upnext-ttl">Product sync</span>
                    <span className="upnext-meta">Meeting Room 3B · Level 3 · Ridge House</span>
                  </div>
                  <div className="upnext-side">
                    <span className="countdown">
                      <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><circle cx="10" cy="10" r="7.2" /><path d="M10 6v4l2.5 1.5" /></svg>
                      In 45 minutes
                    </span>
                    <div className="upnext-actions">
                      <button className="btn sm sec">Reschedule</button>
                      <button className="btn sm quiet">Cancel</button>
                    </div>
                  </div>
                </div>
              </section>

              <section className="card">
                <div className="cardhead">
                  <h2 className="sectiontitle">Your day</h2>
                  <span className="tz">Asia/Amman · shaded band is 07:00 – 20:00</span>
                </div>
                <div className="pad">
                  <div className="daystrip">
                    <div
                      className="ds-open"
                      style={{
                        left: `${(DAY_OPEN_START / 24) * 100}%`,
                        width: `${((DAY_OPEN_END - DAY_OPEN_START) / 24) * 100}%`,
                      }}
                    />
                    <div className="ds-grid" />
                    {DAY_BOOKINGS.map((b) => (
                      <div
                        key={b.label}
                        className="ds-block"
                        style={{ left: `${(b.start / 24) * 100}%`, width: `${((b.end - b.start) / 24) * 100}%` }}
                      >
                        {b.label}
                      </div>
                    ))}
                  </div>
                  <div className="dsticks">
                    <span style={{ left: '0%' }}>00:00</span>
                    <span style={{ left: '25%' }}>06:00</span>
                    <span style={{ left: '50%' }}>12:00</span>
                    <span style={{ left: '75%' }}>18:00</span>
                    <span style={{ left: '100%' }}>24:00</span>
                  </div>
                  <div className="dslegend">
                    <span><span className="dsswatch" style={{ background: 'var(--accent-solid)' }} /> Booked</span>
                    <span><span className="dsswatch" style={{ background: 'var(--surface-raised)', border: '1px solid var(--border-subtle)' }} /> Open hours</span>
                  </div>
                </div>
              </section>

              <section className="card">
                <div className="cardhead">
                  <h2 className="sectiontitle">Upcoming bookings</h2>
                  <span className="btn sm quiet" style={{ opacity: 0.6, cursor: 'default' }}>Open My calendar →</span>
                </div>
                <div className="bklist">
                  {UPCOMING.map((group) => (
                    <div key={group.group}>
                      <div className="daygrp">{group.group}</div>
                      {group.items.map((item) => {
                        const key = `${group.group}-${item.time}`
                        const isCancelling = cancelling === key
                        return (
                          <div className={`bk${isCancelling ? ' cancelling' : ''}`} key={key}>
                            <div className="when">
                              <div className="day">{item.day}</div>
                              <div className="dow">{item.dow}</div>
                            </div>
                            <div className="body">
                              <div className="ttl">{item.title}</div>
                              <div className="t">{item.time}</div>
                              <div className="m">{item.meta}</div>
                            </div>
                            <div className="side2">
                              <button className="btn sm quiet" onClick={() => setCancelling(isCancelling ? null : key)}>
                                {isCancelling ? 'Undo' : 'Cancel'}
                              </button>
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  ))}
                </div>
              </section>
            </div>

            <div className="dashcol">
              <section className="card">
                <div className="cardhead"><h2 className="sectiontitle">Quick book</h2></div>
                <div className="qb">
                  <div className="qbrow">
                    <div className="field">
                      <span className="lbl">Date</span>
                      <input className="ctrl mono" type="date" defaultValue="2026-09-21" min="2026-09-21" max="2026-10-05" />
                    </div>
                    <div className="field">
                      <span className="lbl">Start</span>
                      <select className="ctrl mono" defaultValue="9">
                        {Array.from({ length: 13 }, (_, i) => i + 7).map((h) => (
                          <option key={h} value={h}>{String(h).padStart(2, '0')}:00</option>
                        ))}
                      </select>
                    </div>
                  </div>
                  <div className="field">
                    <span className="lbl">Floor</span>
                    <select className="ctrl" defaultValue="any">
                      <option value="any">Any floor</option>
                      <option value="1">Level 1</option>
                      <option value="2">Level 2</option>
                      <option value="3">Level 3</option>
                      <option value="4">Level 4</option>
                    </select>
                  </div>
                  <div className="field">
                    <span className="lbl">Duration</span>
                    <div className="qbchips">
                      {[0.5, 1, 2].map((d) => (
                        <button
                          key={d}
                          type="button"
                          className={`qbchip${qbDuration === d ? ' on' : ''}`}
                          onClick={() => setQbDuration(d)}
                        >
                          {d === 0.5 ? '30 min' : `${d}h`}
                        </button>
                      ))}
                    </div>
                  </div>
                  <button className="btn" disabled title="Find a space isn't built yet">Find spaces →</button>
                  <p className="hint">Search by date, floor and duration to find an open space.</p>
                </div>
              </section>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
