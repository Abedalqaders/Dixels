// Writes the feature-boundary part of .oxlintrc.json: code may use another feature only through the
// files that feature makes public (listed below), plus every feature's module, permissions and
// queryKeys. Anything else inside another feature fails `oxlint`, and so does a `../` import from
// inside a feature (features import other folders with `@/`, so a relative path can't sneak past).
// Feature folders are found by reading src/features, so a new feature needs no edit here until it
// makes something public. CI reruns this and fails on any difference.
//
// Usage: node scripts/feature-boundaries.mjs   (npm run lint:boundaries)
import { readdirSync, readFileSync, writeFileSync } from 'node:fs'

// The one list: what each feature lets the others use, as paths inside its folder.
const PUBLIC = {
  auth: [
    'components/Can',
    'components/Gate',
    'hooks/useAuthRole',
    'permissions/group',
    'permissions/permissionNames',
    'permissions/usePermission',
    'roles',
    'userManager',
  ],
  bookings: [
    'api/bookingsApi',
    'bookingEvents',
    'buildingRules',
    'components/BookingForm',
    'components/BuildingRemovedNotice',
    'components/EditGuestsDialog',
    'components/FromToFields',
    'components/OwnClashNotice',
    'format',
    'myBuildingLoader',
    'preferences',
    'recurrence',
    'suggestSlot',
  ],
  calendar: ['calendarDates'],
  profile: ['api/profileApi', 'hooks/useMyPicture', 'hooks/useMyProfile'],
  'space-management': [
    'api/spaceManagementApi',
    'components/BuildingPicker',
    'components/spaceTypeIcons',
    'hooks/useBookingImpactPrompt',
  ],
}
const EVERY_FEATURE = ['module', 'permissions', 'queryKeys']

// app/ and main.tsx wire the features together (pages, providers, the module list); tests and the
// shared test helpers mock or fake features' insides (permission contexts, API modules).
const EXEMPT = ['src/app/**', 'src/main.tsx', 'src/**/*.test.ts', 'src/**/*.test.tsx', 'src/test/**']

const MESSAGE =
  "That file is inside another feature. Use one of its public files (scripts/feature-boundaries.mjs), or make this one public there on purpose."
const RELATIVE_MESSAGE = "Import other folders with '@/…', not '../'."

const configFile = new URL('../.oxlintrc.json', import.meta.url)
const features = readdirSync(new URL('../src/features/', import.meta.url), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .sort()

const unknown = Object.keys(PUBLIC).filter((name) => !features.includes(name))
if (unknown.length) throw new Error(`PUBLIC lists features that don't exist: ${unknown.join(', ')}`)

const open = [
  ...EVERY_FEATURE.map((file) => `!@/features/*/${file}`),
  ...Object.entries(PUBLIC).flatMap(([feature, files]) => files.map((file) => `!@/features/${feature}/${file}`)),
]
const rule = (patterns) => ({ 'no-restricted-imports': ['error', { patterns }] })
const boundary = (own) => ({ group: ['@/features/*/**', ...own, ...open], message: MESSAGE })

const overrides = [
  { files: ['src/**'], rules: rule([boundary([])]) },
  ...features.map((feature) => ({
    files: [`src/features/${feature}/**`],
    rules: rule([boundary([`!@/features/${feature}/**`]), { group: ['../**'], message: RELATIVE_MESSAGE }]),
  })),
  { files: EXEMPT, rules: { 'no-restricted-imports': 'off' } },
]

const config = JSON.parse(readFileSync(configFile, 'utf8'))
writeFileSync(configFile, JSON.stringify({ ...config, overrides }, null, 2) + '\n')
