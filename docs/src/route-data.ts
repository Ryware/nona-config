import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const SITE_URL = 'https://nonaconfig.com';
const DOCS_URL = `${SITE_URL}/docs/`;
const REPOSITORY_URL = 'https://github.com/Ryware/nona-config';
const LICENSE_URL = 'https://www.apache.org/licenses/LICENSE-2.0';
const DEFAULT_DESCRIPTION =
	'Self-hosted remote config and feature flags docs for Docker deployment, HTTP access, official clients, and Firebase migration.';

const SECTION_LABELS: Record<string, string> = {
	cli: 'CLI and Migration',
	clients: 'Clients and API',
	comparisons: 'Comparisons',
	concepts: 'Core Concepts',
	deployment: 'Deployment',
	'feature-flags': 'Feature Flags',
	'get-started': 'Get Started',
	migration: 'CLI and Migration',
	operations: 'Operations',
	'remote-config': 'Remote Config',
};

type JsonLdNode = Record<string, unknown>;

function createAbsoluteUrl(pathname: string): string {
	return new URL(pathname, SITE_URL).toString();
}

function createBreadcrumbList(pathname: string, title: string): JsonLdNode | null {
	const docsSegments = pathname
		.replace(/^\/+|\/+$/g, '')
		.split('/')
		.filter(Boolean)
		.filter((segment) => segment !== 'docs');

	const itemListElement = [
		{
			'@type': 'ListItem',
			position: 1,
			name: 'Nona Docs',
			item: DOCS_URL,
		},
	];

	if (docsSegments.length === 0) {
		return null;
	}

	if (docsSegments.length === 1) {
		itemListElement.push({
			'@type': 'ListItem',
			position: 2,
			name: title,
			item: createAbsoluteUrl(pathname),
		});

		return {
			'@type': 'BreadcrumbList',
			itemListElement,
		};
	}

	const sectionLabel = SECTION_LABELS[docsSegments[0]];

	if (sectionLabel) {
		itemListElement.push({
			'@type': 'ListItem',
			position: 2,
			name: sectionLabel,
			item: createAbsoluteUrl(`/docs/${docsSegments[0]}/`),
		});
	}

	itemListElement.push({
		'@type': 'ListItem',
		position: itemListElement.length + 1,
		name: title,
		item: createAbsoluteUrl(pathname),
	});

	return {
		'@type': 'BreadcrumbList',
		itemListElement,
	};
}

function createBaseGraph(language: string): JsonLdNode[] {
	return [
		{
			'@type': 'Organization',
			'@id': `${SITE_URL}/#organization`,
			name: 'Nona Config',
			url: SITE_URL,
			sameAs: [REPOSITORY_URL],
		},
		{
			'@type': 'WebSite',
			'@id': `${DOCS_URL}#website`,
			url: DOCS_URL,
			name: 'Nona Docs',
			description: DEFAULT_DESCRIPTION,
			inLanguage: language,
			publisher: { '@id': `${SITE_URL}/#organization` },
		},
		{
			'@type': 'SoftwareSourceCode',
			'@id': `${DOCS_URL}#software`,
			name: 'Nona',
			url: SITE_URL,
			codeRepository: REPOSITORY_URL,
			license: LICENSE_URL,
			description:
				'Open source, self-hosted remote config and feature flag software for web, mobile, and backend applications.',
			publisher: { '@id': `${SITE_URL}/#organization` },
		},
	];
}

function createPageNode(pathname: string, title: string, description: string, language: string): JsonLdNode {
	return {
		'@type': 'TechArticle',
		'@id': `${createAbsoluteUrl(pathname)}#article`,
		url: createAbsoluteUrl(pathname),
		headline: title,
		name: title,
		description,
		inLanguage: language,
		isPartOf: { '@id': `${DOCS_URL}#website` },
		about: { '@id': `${DOCS_URL}#software` },
		mainEntityOfPage: createAbsoluteUrl(pathname),
		publisher: { '@id': `${SITE_URL}/#organization` },
	};
}

function createHomeNode(pathname: string, title: string, description: string, language: string): JsonLdNode[] {
	const url = createAbsoluteUrl(pathname);
	const topics = [
		{ name: 'Get Started', path: '/docs/get-started/' },
		{ name: 'Feature Flags', path: '/docs/feature-flags/' },
		{ name: 'Remote Config', path: '/docs/remote-config/' },
		{ name: 'Core Concepts', path: '/docs/concepts/' },
		{ name: 'Clients and API', path: '/docs/clients/' },
		{ name: 'Deployment', path: '/docs/deployment/' },
		{ name: 'Operations', path: '/docs/operations/' },
		{ name: 'Migration', path: '/docs/migration/' },
	];

	return [
		{
			'@type': 'CollectionPage',
			'@id': `${url}#webpage`,
			url,
			name: title,
			description,
			inLanguage: language,
			isPartOf: { '@id': `${DOCS_URL}#website` },
			about: { '@id': `${DOCS_URL}#software` },
			publisher: { '@id': `${SITE_URL}/#organization` },
		},
		{
			'@type': 'ItemList',
			'@id': `${url}#topics`,
			name: 'Nona documentation topics',
			itemListElement: topics.map((topic, index) => ({
				'@type': 'ListItem',
				position: index + 1,
				name: topic.name,
				item: createAbsoluteUrl(topic.path),
			})),
		},
	];
}

export const onRequest = defineRouteMiddleware((context) => {
	const route = context.locals.starlightRoute;
	const pathname = context.url.pathname;
	const title = route.entry.data.title;
	const description = route.entry.data.description ?? DEFAULT_DESCRIPTION;
	const language = route.lang;
	const graph = createBaseGraph(language);

	if (route.id === '') {
		graph.push(...createHomeNode(pathname, title, description, language));
	} else {
		graph.push(createPageNode(pathname, title, description, language));

		const breadcrumbs = createBreadcrumbList(pathname, title);

		if (breadcrumbs) {
			graph.push(breadcrumbs);
		}
	}

	route.head.push({
		tag: 'script',
		attrs: {
			type: 'application/ld+json',
		},
		content: JSON.stringify({
			'@context': 'https://schema.org',
			'@graph': graph,
		}),
	});
});
