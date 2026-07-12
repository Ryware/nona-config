// @ts-check
import { defineConfig } from 'astro/config';
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
		starlight({
			title: 'Nona Docs',
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/Ryware/nona-config' }],
			sidebar: [
				{
					label: 'Admin',
					items: [
						{ label: 'Parameter share links', slug: 'parameter-share-links' },
					],
				},
				{
					label: 'Deployment',
					items: [
						{ label: 'Standalone production', slug: 'deployment/standalone' },
						{ label: 'Primary/replica production', slug: 'deployment/primary-replica' },
					],
				},
				{
					label: 'Clients',
					items: [
						{ label: 'HTTP', slug: 'clients/http' },
						{ label: 'JavaScript', slug: 'clients/javascript' },
						{ label: '.NET', slug: 'clients/dotnet' },
					],
				},
				{
					label: 'CLI',
					items: [
						{ label: 'Overview', slug: 'cli' },
						{ label: 'Firebase migration', slug: 'cli/firebase-migration' },
						{ label: 'Reference', slug: 'cli/reference' },
					],
				},
			],
		}),
	],
});
