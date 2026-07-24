// @ts-check
import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	site: 'https://nonaconfig.com',
	base: '/docs',
	vite: {
		server: {
			allowedHosts: ['nona-standalone--nona--cutehorse256.dev3.ahorse.top'],
		},
	},
	integrations: [
		sitemap(),
		starlight({
			title: 'Nona Docs',
			components: {
				Footer: './src/components/Footer.astro',
			},
			routeMiddleware: './src/route-data.ts',
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/Ryware/nona-config' }],
			sidebar: [
				{
					label: 'Overview',
					items: [
						{ label: 'Why Nona', slug: 'why-nona' },
					],
				},
				{
					label: 'Get Started',
					items: [
						{ label: 'Overview', slug: 'get-started' },
						{ label: 'Deploy with Docker', slug: 'get-started/docker' },
						{ label: 'Create your first project', slug: 'get-started/first-project' },
						{ label: 'Add your first parameter', slug: 'get-started/first-parameter' },
						{ label: 'Create an API key', slug: 'get-started/api-keys' },
						{ label: 'Fetch your first config value', slug: 'get-started/first-api-call' },
						{ label: 'Add a kill switch', slug: 'get-started/kill-switch' },
					],
				},
				{
					label: 'Feature Flags',
					items: [
						{ label: 'Overview', slug: 'feature-flags' },
						{ label: 'What are feature flags?', slug: 'feature-flags/what-are-feature-flags' },
						{ label: 'Feature flags vs remote config', slug: 'feature-flags/feature-flags-vs-remote-config' },
						{ label: 'Kill switches', slug: 'feature-flags/kill-switches' },
						{ label: 'Feature flags for mobile apps', slug: 'feature-flags/mobile-app-feature-flags' },
						{ label: 'Feature flags for backend services', slug: 'feature-flags/backend-feature-flags' },
					],
				},
				{
					label: 'Remote Config',
					items: [
						{ label: 'Overview', slug: 'remote-config' },
						{ label: 'What is remote config?', slug: 'remote-config/what-is-remote-config' },
						{ label: 'Remote config vs environment variables', slug: 'remote-config/remote-config-vs-environment-variables' },
						{ label: 'Remote config for mobile apps', slug: 'remote-config/mobile-app-remote-config' },
						{ label: 'Remote config use cases', slug: 'remote-config/use-cases' },
						{ label: 'Server-side remote config', slug: 'remote-config/server-side-remote-config' },
					],
				},
				{
					label: 'Core Concepts',
					items: [
						{ label: 'Overview', slug: 'concepts' },
						{ label: 'Projects', slug: 'concepts/projects' },
						{ label: 'Environments', slug: 'concepts/environments' },
						{ label: 'Releases', slug: 'concepts/releases' },
						{ label: 'Parameters and content types', slug: 'concepts/parameters-and-content-types' },
						{ label: 'Client vs server scope', slug: 'concepts/client-vs-server-scope' },
						{ label: 'API keys', slug: 'concepts/api-keys' },
						{ label: 'History and rollback', slug: 'concepts/history-and-rollback' },
						{ label: 'Audit logs', slug: 'concepts/audit-logs' },
						{ label: 'Parameter share links', slug: 'parameter-share-links' },
						{ label: 'Users and project access', slug: 'concepts/users-and-project-access' },
					],
				},
				{
					label: 'Clients and API',
					items: [
						{ label: 'Overview', slug: 'clients' },
						{ label: 'HTTP', slug: 'clients/http' },
						{ label: 'JavaScript', slug: 'clients/javascript' },
						{ label: '.NET', slug: 'clients/dotnet' },
						{ label: 'OpenFeature', slug: 'clients/openfeature' },
					],
				},
				{
					label: 'CLI and Migration',
					items: [
						{ label: 'CLI overview', slug: 'cli' },
						{ label: 'CLI reference', slug: 'cli/reference' },
						{ label: 'Migration overview', slug: 'migration' },
						{ label: 'Migrate from Firebase Remote Config', slug: 'migration/firebase-remote-config' },
						{ label: 'Firebase concept mapping', slug: 'migration/firebase-concept-mapping' },
						{ label: 'Migration validation', slug: 'migration/validation' },
					],
				},
				{
					label: 'Deployment',
					items: [
						{ label: 'Overview', slug: 'deployment' },
						{ label: 'Standalone production', slug: 'deployment/standalone' },
						{ label: 'Primary/replica production', slug: 'deployment/primary-replica' },
					],
				},
				{
					label: 'Comparisons',
					items: [
						{ label: 'Open source feature flags', slug: 'comparisons/open-source-feature-flags' },
						{ label: 'Self-hosted feature flags', slug: 'comparisons/self-hosted-feature-flags' },
						{ label: 'Open source remote config', slug: 'comparisons/open-source-remote-config' },
						{ label: 'Firebase Remote Config alternative', slug: 'comparisons/firebase-remote-config-alternative' },
					],
				},
				{
					label: 'Operations',
					items: [
						{ label: 'Overview', slug: 'operations' },
						{ label: 'Security and authentication', slug: 'operations/security-and-authentication' },
						{ label: 'Single sign-on (SSO)', slug: 'operations/sso' },
						{ label: 'Backups', slug: 'operations/backups' },
						{ label: 'Upgrades', slug: 'operations/upgrades' },
					],
				},
			],
		}),
	],
});
