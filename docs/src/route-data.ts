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
type FaqItem = { question: string; answer: string };
type HowToStepItem = { name: string; text: string };

const PAGE_FAQS: Record<string, FaqItem[]> = {
	'comparisons/firebase-remote-config-alternative': [
		{
			question: 'Is Nona open source?',
			answer:
				'Yes. Nona is open source and self-hosted, which makes it materially different from Firebase Remote Config.',
		},
		{
			question: 'Can Nona handle both feature flags and remote config?',
			answer:
				'Yes. In Nona, boolean entries work naturally as feature flags, while text, number, and json entries cover broader runtime configuration.',
		},
		{
			question: 'Does Nona use the same product model as Firebase Remote Config?',
			answer:
				'No. Nona solves a similar problem space, but it does not keep the same hosted-platform model and should not be explained as a one-to-one Firebase clone.',
		},
		{
			question: 'How should I evaluate Nona as a Firebase Remote Config replacement?',
			answer:
				'Start small: run the Docker image, create one project and environment, add one flag and one non-boolean parameter, then test a real read before planning a full migration.',
		},
	],
	'comparisons/open-source-feature-flags': [
		{
			question: 'Is Nona really open source?',
			answer:
				'Yes. Nona is open source and self-hosted, which makes it a strong fit for teams that want infrastructure control instead of a hosted flag platform.',
		},
		{
			question: 'Can I use Nona just for feature flags?',
			answer:
				'Yes. Many teams start with boolean flags and kill switches first, then expand into broader runtime configuration later.',
		},
		{
			question: 'Does Nona support backend and frontend flags?',
			answer:
				'Yes. The scope model separates client, server, and shared reads so the same system can support frontend, mobile, and backend use cases.',
		},
		{
			question: 'When is Nona a better fit than a larger flag platform?',
			answer:
				'Nona is usually the better fit when you want self-hosting, simpler operations, and one product for feature flags and remote config without committing to a larger hosted control plane.',
		},
	],
	'comparisons/open-source-remote-config': [
		{
			question: 'Is Nona only for remote config?',
			answer:
				'No. Nona supports remote config and feature flags in the same system, which is useful when teams want one operational surface instead of separate tools.',
		},
		{
			question: 'Can Nona be used for backend services?',
			answer:
				'Yes. Nona works for web, mobile, and backend applications instead of only mobile-SDK-centric flows.',
		},
		{
			question: 'Do I need an SDK to read values?',
			answer:
				'No. You can use plain HTTP directly, or use the official JavaScript and .NET clients if you prefer.',
		},
		{
			question: 'When is Nona a strong fit for open source remote config?',
			answer:
				'Nona is a strong fit when you want open source remote config, self-hosting, plain HTTP access, and feature flags in the same product.',
		},
	],
	'comparisons/self-hosted-feature-flags': [
		{
			question: 'What makes Nona self-hosted?',
			answer:
				'You deploy and operate the Nona service yourself, which means your team controls where it runs, how it is upgraded, and how applications access it.',
		},
		{
			question: 'Does Nona require a hosted vendor control plane?',
			answer:
				'No. Nona is designed to run on infrastructure you control, which is why it fits teams looking for self-hosted feature flags.',
		},
		{
			question: 'Can self-hosted Nona handle kill switches?',
			answer:
				'Yes. Boolean entries work naturally as feature flags and kill switches, and they can be scoped to the correct read surface.',
		},
		{
			question: 'Is Nona trying to be a full experimentation platform?',
			answer:
				'No. Nona is better positioned as a simpler self-hosted system for feature flags and remote config, not a giant experimentation suite.',
		},
	],
	'feature-flags': [
		{
			question: 'Is Nona only a feature flag tool?',
			answer:
				'No. Nona supports feature flags and broader remote config in the same system. Feature flags are one major use case, not the whole product.',
		},
		{
			question: 'How do feature flags work in Nona?',
			answer:
				'Most feature flags in Nona are boolean config entries. That gives teams a simple operational model for toggles, kill switches, and release gates.',
		},
		{
			question: 'Can Nona handle backend and frontend flags?',
			answer:
				'Yes. The scope model allows client-readable, server-only, and shared reads depending on where the flag should be evaluated.',
		},
		{
			question: 'When is Nona a good fit for feature flags?',
			answer:
				'Nona is a strong fit when you want self-hosted, open source feature flags with simpler operations and one product for flags and runtime config.',
		},
	],
	'migration/firebase-remote-config': [
		{
			question: 'Should I run a dry run before importing?',
			answer:
				'Yes. A dry run is the safest first step because it shows how Firebase data will map into Nona before anything is written to the target project.',
		},
		{
			question: 'Do Firebase conditions stay as runtime conditions in Nona?',
			answer:
				'No. Firebase conditions are source-side migration concepts. In Nona, they are mapped into explicit environments during import.',
		},
		{
			question: 'Will Firebase boolean parameters still work as feature flags?',
			answer:
				'Yes. Boolean Firebase values map into Nona boolean entries, so they continue to work naturally as feature flags after import.',
		},
		{
			question: 'Is the migration done as soon as the import command succeeds?',
			answer:
				'No. You still need to validate environments, scopes, content types, and real application reads before the cutover is complete.',
		},
	],
	'migration': [
		{
			question: 'Is migration just an export and import task?',
			answer:
				'No. For Nona, migration is also a model transition from Firebase concepts into projects, environments, scopes, and typed entries.',
		},
		{
			question: 'What is the first migration command I should run?',
			answer:
				'Start with a dry run using nona migrate firebase --config ./nona.migration.json --dry-run so you can inspect how the source data will land in Nona.',
		},
		{
			question: 'Do Firebase boolean parameters stay useful after migration?',
			answer:
				'Yes. Boolean Firebase values map naturally into Nona boolean entries, which means they continue to work as feature flags after import.',
		},
		{
			question: 'When is the migration actually complete?',
			answer:
				'Only after you validate environments, scopes, datatypes, and real application reads, not just after the import command succeeds.',
		},
	],
	'remote-config': [
		{
			question: 'Is Nona only for remote config?',
			answer:
				'No. Nona supports remote config and feature flags in the same system, which is one of its important product differences.',
		},
		{
			question: 'Can Nona be used for backend services?',
			answer:
				'Yes. Nona works for backend services as well as web and mobile applications, which is why server-side remote config is a first-class docs path.',
		},
		{
			question: 'Do I need an SDK to use Nona remote config?',
			answer:
				'No. You can read values directly over HTTP, or use the official JavaScript and .NET clients if that fits the application better.',
		},
		{
			question: 'When is remote config better than environment variables?',
			answer:
				'Remote config is better when values need to change after deployment, differ by environment at runtime, or support operational control without a redeploy.',
		},
	],
	'get-started/docker': [
		{
			question: 'Do I need Docker Compose to deploy Nona?',
			answer:
				'No. The preferred first deployment path is a single Docker container. Compose is useful for local or team-managed setups, but it is not required for the default first deployment.',
		},
		{
			question: 'What data must persist?',
			answer:
				'Persist /var/lib/nona. That volume holds the local state the container needs to keep your Nona instance intact.',
		},
		{
			question: 'What should I do right after the container starts?',
			answer:
				'Open the admin UI, create the first account, create a project and environment, then add a parameter and test a real read.',
		},
		{
			question: 'When should I move to the production deployment guides?',
			answer:
				'Move to the production deployment guides once the single-container flow works and you are ready to harden the deployment.',
		},
	],
	'get-started/first-project': [
		{
			question: 'How many environments should I create first?',
			answer:
				'In most cases, start with two: one non-production environment such as staging and one production environment.',
		},
		{
			question: 'Should I create environments in the CLI or admin?',
			answer:
				'For the current documented flow, create the project wherever you prefer, then create environments in the admin UI.',
		},
		{
			question: 'Should one app get one project?',
			answer:
				'Usually yes. A Nona project is a good boundary for one application or service and the keys, environments, API keys, and history that belong to it.',
		},
		{
			question: 'What should I do right after the project exists?',
			answer:
				'Add your first parameter so the project starts holding real config or feature flags.',
		},
	],
	'get-started/first-api-call': [
		{
			question: 'Why is the project name not in the HTTP path?',
			answer:
				'The API key already scopes the request to a project, so the path only needs the environment id and key.',
		},
		{
			question: 'Do I need to URL-encode the key?',
			answer:
				'Yes. Keys such as Features:Checkout must be URL-encoded in the path, for example as Features%3ACheckout.',
		},
		{
			question: 'Should I test over HTTP before using an SDK?',
			answer:
				'Yes in most cases. A direct HTTP read is the simplest way to prove the instance, key, environment, and API key are all aligned before adding SDK code.',
		},
		{
			question: 'What should I do after the first successful read?',
			answer:
				'Either keep using direct HTTP for a very small integration, or move to the JavaScript or .NET client for application code.',
		},
	],
	'get-started/first-parameter': [
		{
			question: 'What is the best first parameter to create?',
			answer:
				'A boolean flag such as Features:Checkout is usually the easiest first choice because it is simple to verify and demonstrates the feature-flag side of Nona immediately.',
		},
		{
			question: 'When should I use boolean instead of text?',
			answer:
				'Use boolean when the value is really acting as a flag or kill switch. If the value is freeform content or a label, use text instead.',
		},
		{
			question: 'Should I use client, server, or all first?',
			answer:
				'Use the narrowest scope that matches the real read surface. For many frontend or mobile tests, client is the easiest first scope. For backend-only values, use server.',
		},
		{
			question: 'Should I start with a JSON value?',
			answer:
				'Usually no. A simple boolean or text value is easier to validate first. Add JSON once the basic read path is already working.',
		},
	],
	'get-started/api-keys': [
		{
			question: 'Should I create one broad key for everything?',
			answer:
				'No. It is better to create a narrowly scoped key for each real application read surface than to reuse one broad key everywhere.',
		},
		{
			question: 'Should frontend apps use all scope?',
			answer:
				'Usually no. Frontend and mobile apps should usually start with client unless there is a real reason they need broader access.',
		},
		{
			question: 'Do I need a separate key per environment?',
			answer:
				'Often yes. Environment scoping is a useful way to avoid accidental cross-environment reads and to keep access narrower.',
		},
		{
			question: 'What should I do if a key works in admin but not in the app?',
			answer:
				'Check the project, environment, and scope alignment first. Those are the most common reasons a read fails after the key is created.',
		},
	],
	'get-started/kill-switch': [
		{
			question: 'What is the best first kill switch candidate?',
			answer:
				'A risky but easy-to-disable feature path is usually best, such as new checkout logic or a third-party integration.',
		},
		{
			question: 'Should a kill switch always be boolean?',
			answer:
				'Usually yes. Boolean values are the clearest fit for kill switches because the operational action is typically just on or off.',
		},
		{
			question: 'Should the kill switch be client or server?',
			answer:
				'It depends on where the app evaluates the flag. Use client for frontend or mobile checks, server for backend-only behavior, and all only when both sides genuinely need to read it.',
		},
		{
			question: 'What makes a kill switch operationally useful?',
			answer:
				'The off path must actually be safe and tested. If disabling the flag still breaks the feature or application, the kill switch is not doing the job you need during an incident.',
		},
	],
	'migration/validation': [
		{
			question: 'Is a successful import enough to declare the migration done?',
			answer:
				'No. A successful import only proves the write step completed. You still need to validate environments, scopes, datatypes, and real reads before production cutover.',
		},
		{
			question: 'What should I validate first?',
			answer:
				'Start with high-risk values such as kill switches, release flags, backend-only values, and production-only settings.',
		},
		{
			question: 'Should I validate only in the admin UI?',
			answer:
				'No. The admin UI is useful for inspection, but you also need at least one real read path through HTTP or a client SDK to prove runtime behavior.',
		},
		{
			question: 'What is the most common migration mistake to catch here?',
			answer:
				'A value landing in the wrong environment or with the wrong scope. That kind of issue can survive a technically successful import and still break real application behavior.',
		},
	],
};

const PAGE_HOW_TOS: Record<string, HowToStepItem[]> = {
	'get-started/docker': [
		{
			name: 'Run the container',
			text: 'Start the single-container Nona deployment with docker run and a persistent /var/lib/nona volume.',
		},
		{
			name: 'Open the admin UI',
			text: 'Visit http://localhost:18080/register and create the first admin account.',
		},
		{
			name: 'Create the initial project and environment',
			text: 'Open Projects, create or open a project, add at least one environment such as staging or production, then add a parameter.',
		},
		{
			name: 'Create an API key',
			text: 'Create a project or environment API key so an application can read configuration from the running instance.',
		},
		{
			name: 'Verify a real read',
			text: 'Test one HTTP read or client SDK read to confirm the deployment is usable by an application.',
		},
	],
	'get-started/first-project': [
		{
			name: 'Open the Projects screen',
			text: 'Sign in to the admin UI and open Projects.',
		},
		{
			name: 'Create the project',
			text: 'Create the project that will hold the configuration for your application or service.',
		},
		{
			name: 'Open the project',
			text: 'Open the newly created project so you can manage its environments.',
		},
		{
			name: 'Create staging',
			text: 'Add a non-production environment such as staging so you have a safe place to test values.',
		},
		{
			name: 'Create production',
			text: 'Add the production environment and verify both environments appear as tabs on the project page.',
		},
	],
	'get-started/first-api-call': [
		{
			name: 'Confirm the parameter exists',
			text: 'Make sure the target parameter has already been created in the correct environment.',
		},
		{
			name: 'Create or confirm the API key',
			text: 'Create an API key with the correct project and read scope for the target environment.',
		},
		{
			name: 'Prepare the request path',
			text: 'Copy the environment id and URL-encode the key name before building the request URL.',
		},
		{
			name: 'Send the request',
			text: 'Make the HTTP request with the X-Api-Key header against the running Nona instance.',
		},
		{
			name: 'Verify the response',
			text: 'Confirm the correct value is returned so you know the instance, environment, key, and API key are aligned.',
		},
	],
	'get-started/first-parameter': [
		{
			name: 'Open the project and environment',
			text: 'Open the target project and select the environment where the parameter should live.',
		},
		{
			name: 'Create the parameter',
			text: 'Click Add Parameter and enter a key such as Features:Checkout.',
		},
		{
			name: 'Choose the datatype',
			text: 'Pick the content type that matches the real value, such as boolean for a feature flag.',
		},
		{
			name: 'Choose the scope',
			text: 'Select the narrowest correct scope so only the intended read surface can access the value.',
		},
		{
			name: 'Save and verify',
			text: 'Save the parameter and confirm it appears in the environment table or through a CLI read.',
		},
	],
	'get-started/api-keys': [
		{
			name: 'Open the target project',
			text: 'Open the project that the application will use for reads.',
		},
		{
			name: 'Choose the narrowest scope',
			text: 'Pick client, server, or all based on the actual application read surface.',
		},
		{
			name: 'Optionally limit the environment',
			text: 'Set the environment constraint if the key only needs to read one environment such as production.',
		},
		{
			name: 'Create and copy the key',
			text: 'Create the key and copy the generated value immediately before leaving the page.',
		},
		{
			name: 'Test a real read',
			text: 'Use the key in one HTTP request so you know the project, environment, and scope are aligned correctly.',
		},
	],
	'get-started/kill-switch': [
		{
			name: 'Create the boolean flag',
			text: 'Create a boolean parameter such as Features:Checkout in the target environment.',
		},
		{
			name: 'Set the initial state',
			text: 'Set the initial value to true and choose the correct scope for where the feature is evaluated.',
		},
		{
			name: 'Wire the application',
			text: 'Make sure the application respects both the enabled and disabled states of the flag.',
		},
		{
			name: 'Test the off path',
			text: 'Verify the safe disabled behavior before you ever need the kill switch during an incident.',
		},
		{
			name: 'Flip the flag when needed',
			text: 'Set the value to false during an incident and use history or rollback if you need to review or restore earlier states.',
		},
	],
};

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

function createFaqNode(pathname: string, faqs: FaqItem[]): JsonLdNode {
	return {
		'@type': 'FAQPage',
		'@id': `${createAbsoluteUrl(pathname)}#faq`,
		url: createAbsoluteUrl(pathname),
		mainEntity: faqs.map((item) => ({
			'@type': 'Question',
			name: item.question,
			acceptedAnswer: {
				'@type': 'Answer',
				text: item.answer,
			},
		})),
	};
}

function createHowToNode(pathname: string, title: string, description: string, steps: HowToStepItem[]): JsonLdNode {
	return {
		'@type': 'HowTo',
		'@id': `${createAbsoluteUrl(pathname)}#howto`,
		url: createAbsoluteUrl(pathname),
		name: title,
		description,
		step: steps.map((step, index) => ({
			'@type': 'HowToStep',
			position: index + 1,
			name: step.name,
			text: step.text,
			url: createAbsoluteUrl(pathname),
		})),
	};
}

export const onRequest = defineRouteMiddleware((context) => {
	const route = context.locals.starlightRoute;
	const pathname = context.url.pathname;
	const routeId = route.id;
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

	const faqs = PAGE_FAQS[routeId];

	if (faqs) {
		graph.push(createFaqNode(pathname, faqs));
	}

	const howToSteps = PAGE_HOW_TOS[routeId];

	if (howToSteps) {
		graph.push(createHowToNode(pathname, title, description, howToSteps));
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
