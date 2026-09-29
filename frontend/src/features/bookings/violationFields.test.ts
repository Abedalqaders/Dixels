import { describe, expect, it } from 'vitest'
import { groupViolations, issueText } from './violationFields'

const v = (code: string, message = code) => ({ code, level: null, message, shortMessage: message })

describe('groupViolations', () => {
  it('sends each rule to the field it is about, keeping the server order within a field', () => {
    const issues = groupViolations([
      v('Dixels:Bookings:BelowMinAttendees', 'Needs 2 people.'),
      v('Dixels:Bookings:OutsideHours', 'Outside hours.'),
      v('Dixels:Bookings:ClosedDay', 'Closed that day.'),
      v('Dixels:Bookings:TooLong', 'Too long.'),
      v('Dixels:Bookings:OverCapacity', 'Seats 8.'),
    ])

    expect(issues.date.map((x) => x.message)).toEqual(['Closed that day.'])
    expect(issues.time.map((x) => x.message)).toEqual(['Outside hours.', 'Too long.'])
    expect(issues.attendees.map((x) => x.message)).toEqual(['Needs 2 people.', 'Seats 8.'])
    expect(issues.other).toEqual([])
  })

  it('keeps a rule it does not know in the panel rather than guessing a field', () => {
    const issues = groupViolations([v('Dixels:Bookings:SomethingNew', 'New rule.')])
    expect(issues.other.map((x) => x.message)).toEqual(['New rule.'])
    expect(issueText(issues.other)).toBe('New rule.')
    expect(issueText(issues.time)).toBeNull()
  })
})
