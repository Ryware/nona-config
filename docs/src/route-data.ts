import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const SITE_URL = 'https://nonaconfig.com';
const DOCS_URL = `${SITE_URL}/docs`;
const OG_IMAGE = `${SITE_URL}/opengraph-image`;
const OG_IMAGE_ALT = 'Nona — open source self-hosted remote config and feature flags';
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
			question: 'Does Nona support targeting or percentage rollout?',
			answer:
				'No. Nona is not a runtime targeting engine. It does not evaluate flags against user context, segments, cohorts, or percentage rules on the read path.',
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
	'clients': [
		{
			question: 'What is the best first integration path?',
			answer:
				'HTTP is usually the best first validation path because it proves the instance, key, environment, and API key all work before client code gets involved.',
		},
		{
			question: 'When should I use JavaScript or .NET instead of HTTP?',
			answer:
				'Use the language client when you want a more convenient integration surface, caching behavior, or typed helpers instead of manual request handling.',
		},
		{
			question: 'When should I use OpenFeature?',
			answer:
				'Use OpenFeature when your team wants a vendor-neutral feature-flag interface or already uses OpenFeature across multiple applications.',
		},
		{
			question: 'Do all integration paths still use the same Nona model?',
			answer:
				'Yes. All integration paths still rely on the same underlying Nona project, environment, scope, and API key model.',
		},
	],
	'clients/http': [
		{
			question: 'When should I use raw HTTP instead of a client?',
			answer:
				'Use raw HTTP when you want the smallest possible integration path, are working in a language without an official client, or are validating the instance during setup or migration.',
		},
		{
			question: 'Why is the response body so simple?',
			answer:
				'The endpoint returns the raw stored value in the body and the logical type in the X-Nona-Content-Type header so it stays easy to consume from almost any language.',
		},
		{
			question: 'Do I always need to URL-encode the key?',
			answer:
				'Yes. Keys such as Features:Checkout must be encoded in the path, for example as Features%3ACheckout.',
		},
		{
			question: 'What should I check first when a request fails?',
			answer:
				'Start with the environment name, key existence, URL encoding, API key project, and scope alignment.',
		},
		{
			question: 'Can I pass userId or X-User-Id to get a targeted value?',
			answer:
				'No. The HTTP read path is a direct environment-and-key lookup. Nona does not perform built-in user targeting, segmentation, cohort evaluation, or percentage rollout.',
		},
	],
	'clients/javascript': [
		{
			question: 'When should I use the JavaScript client instead of raw HTTP?',
			answer:
				'Use the JavaScript client when you want a direct client API, optional TTL cache behavior, and less manual request handling than raw HTTP.',
		},
		{
			question: 'Should I read feature flags as strings?',
			answer:
				'Usually no. For real flags, it is better to read the config value and inspect contentType so the application stays aligned with Nona logical types.',
		},
		{
			question: 'Is caching required?',
			answer:
				'No. The JavaScript client cache is optional and disabled by default. Only enable it when repeated reads justify it.',
		},
		{
			question: 'When should I move to OpenFeature?',
			answer:
				'Move to OpenFeature when the app becomes more flag-oriented and you want a vendor-neutral interface instead of direct Nona-specific reads.',
		},
	],
	'clients/dotnet': [
		{
			question: 'When should I use the .NET client instead of raw HTTP?',
			answer:
				'Use the .NET client when you want a direct C# integration, built-in cache behavior, typed JSON reads, and a simpler path than maintaining your own HTTP wrapper.',
		},
		{
			question: 'Does the .NET client cache values?',
			answer:
				'Yes. The .NET client caches values in memory by default, and you can tune the TTL and memory limit through NonaClientOptions.',
		},
		{
			question: 'Should backend services prefer server scope?',
			answer:
				'Usually yes. Backend-only values should use server scope whenever possible so the read surface stays as narrow as possible.',
		},
		{
			question: 'When should I use the OpenFeature provider?',
			answer:
				'Use the OpenFeature provider when the service is becoming more flag-oriented and you want a vendor-neutral evaluation interface on top of the Nona client.',
		},
	],
	'clients/openfeature': [
		{
			question: 'When is OpenFeature a better fit than the direct client?',
			answer:
				'OpenFeature is a better fit when your team already uses OpenFeature or wants a vendor-neutral feature-flag interface instead of direct Nona-specific reads.',
		},
		{
			question: 'Does OpenFeature replace Nona operational model?',
			answer:
				'No. OpenFeature only changes the application-side interface. Nona still provides the projects, environments, scopes, API keys, and history underneath.',
		},
		{
			question: 'Should I start with OpenFeature immediately?',
			answer:
				'Usually only if your team already thinks in OpenFeature terms. Otherwise, many teams start with the direct client or raw HTTP first.',
		},
		{
			question: 'What is the best first OpenFeature test?',
			answer:
				'Resolve one boolean flag such as Features:Checkout and verify that the application sees the value change after you edit it in Nona.',
		},
	],
	'cli': [
		{
			question: 'When should I use the CLI instead of the admin UI?',
			answer:
				'Use the CLI for repeatable operations, scripting, migration work, history and rollback workflows, and terminal-first administration.',
		},
		{
			question: 'Do I still need the admin UI if I use the CLI?',
			answer:
				'Often yes. Some workflows such as environment creation are still documented primarily through the admin UI.',
		},
		{
			question: 'What is the best first CLI command to run?',
			answer:
				'After installation, nona auth login --base-url https://nona.example.com is usually the best first command because it establishes the interactive session.',
		},
		{
			question: 'Why is the CLI especially important for Firebase migration?',
			answer:
				'Because migration is an operator workflow that benefits from dry runs, config files, and repeatable execution from a terminal.',
		},
	],
	'cli/firebase-migration': [
		{
			question: 'Should I run a dry run first?',
			answer:
				'Yes. The dry run is the safest first step because it shows how the migration will land before writing anything to Nona.',
		},
		{
			question: 'Do Firebase booleans stay useful after migration?',
			answer:
				'Yes. They map naturally into Nona boolean entries and continue to work well as feature flags.',
		},
		{
			question: 'What happens to Firebase conditions?',
			answer:
				'They are mapped into Nona environments during migration instead of staying as Firebase-style runtime targeting rules.',
		},
		{
			question: 'What is the biggest risk in this migration flow?',
			answer:
				'Assuming a technically successful import means the migration is finished. You still need to validate environments, scopes, content types, and real application reads afterward.',
		},
	],
	'': [
		{
			question: 'What is Nona in one sentence?',
			answer:
				'Nona is an open source, self-hosted remote config and feature flag service for web, mobile, and backend applications.',
		},
		{
			question: 'Is Nona only for remote config?',
			answer:
				'No. Nona supports both feature flags and broader runtime config in the same system.',
		},
		{
			question: 'What is the fastest way to understand whether Nona fits my team?',
			answer:
				'Deploy the Docker image, create one project, add one boolean flag and one non-boolean value, then read both over HTTP or a client.',
		},
		{
			question: 'Who is Nona best for?',
			answer:
				'Teams that want self-hosted runtime control, plain HTTP access, Docker-first deployment, and a smaller product model than a hosted control plane are usually a strong fit.',
		},
		{
			question: 'Does Nona support per-user targeting or percentage rollout?',
			answer:
				'No. Nona is remote config plus simple on/off flags, not a runtime targeting engine. It does not evaluate reads against userId, request context, segments, cohorts, or percentage rules.',
		},
	],
	'concepts': [
		{
			question: 'What is the most important Nona concept to understand first?',
			answer:
				'Start with the project, environment, entry, scope, and API key model. Once those are clear, the rest of the product becomes much easier to reason about.',
		},
		{
			question: 'Are these concepts only for remote config?',
			answer:
				'No. They support both major Nona use cases: feature flags and broader remote config.',
		},
		{
			question: 'Why does Nona emphasize a small core model?',
			answer:
				'Because a smaller model is easier to operate, document, and reason about in production. That is part of the product position.',
		},
		{
			question: 'What should I do if the concepts still feel abstract?',
			answer:
				'Run through one real setup flow with a project, two environments, one flag, one text value, and one API key. That usually makes the terminology concrete quickly.',
		},
	],
	'concepts/projects': [
		{
			question: 'Should one app always map to one project?',
			answer:
				'Usually yes. One project per app or service boundary is the clearest starting model for keys, environments, and access.',
		},
		{
			question: 'When should I split into multiple projects?',
			answer:
				'Split when apps should not share API keys, environments, ownership, or access boundaries.',
		},
		{
			question: 'Can one project contain both feature flags and remote config?',
			answer:
				'Yes. That is a normal and intended Nona usage pattern.',
		},
		{
			question: 'What is the most common project mistake?',
			answer:
				'Creating too many projects too early. If the same app and team own the values, one clear project is usually better than several tiny ones.',
		},
	],
	'concepts/environments': [
		{
			question: 'How many environments should most teams start with?',
			answer:
				'Most teams should start with staging and production. That is enough to test safely without creating unnecessary environment sprawl.',
		},
		{
			question: 'Should environment names match real operational stages?',
			answer:
				'Yes. Environment names should map to real runtime stages that your team actually uses.',
		},
		{
			question: 'Can the same key exist in multiple environments?',
			answer:
				'Yes. That is one of the main reasons environments exist. The key stays stable while the value changes by stage.',
		},
		{
			question: 'Are Firebase conditions the same thing as Nona environments?',
			answer:
				'No. Firebase conditions can be mapped into Nona environments during migration, but Nona environments are not a Firebase-style runtime targeting engine.',
		},
	],
	'concepts/releases': [
		{
			question: 'Does editing parameters immediately affect clients?',
			answer:
				'No. Editing changes the working configuration only. Clients read releases, either the active release or an explicitly requested version.',
		},
		{
			question: 'Why does Create a version ask for 1.2 instead of 1.2.0?',
			answer:
				'Because the admin flow treats the first release in a line as patch .0 automatically, which keeps version entry simpler while still storing full release versions.',
		},
		{
			question: 'Does creating a release activate it automatically?',
			answer:
				'No. Activation is a separate deliberate action on the Releases page.',
		},
		{
			question: 'What does Amend do?',
			answer:
				'Amend loads an existing release into the working configuration and targets the next free patch in that same line.',
		},
	],
	'concepts/parameters-and-content-types': [
		{
			question: 'What is the best first parameter type to create?',
			answer:
				'Usually a boolean flag or a simple text value. Those are the easiest shapes to validate during the first integration.',
		},
		{
			question: 'When should I use json instead of separate keys?',
			answer:
				'Use json when the values naturally belong together and the client consumes them as one structured object.',
		},
		{
			question: 'Does content type control who can read the value?',
			answer:
				'No. Content type describes the value shape. Scope controls who can read it.',
		},
		{
			question: 'What is the most common datatype mistake?',
			answer:
				'Storing a real feature flag as text instead of boolean.',
		},
	],
	'concepts/client-vs-server-scope': [
		{
			question: 'What scope should I choose first?',
			answer:
				'Choose the narrowest scope that matches the real read surface. For many frontend or mobile reads, that is client. For backend-only values, that is server.',
		},
		{
			question: 'When should I use all?',
			answer:
				'Only when both frontend and backend genuinely need to read the same value. It should be the exception, not the default.',
		},
		{
			question: 'Can a boolean flag be server scope?',
			answer:
				'Yes. Feature flags are not automatically client-side. A boolean flag can be client, server, or all depending on where it is evaluated.',
		},
		{
			question: 'What is the biggest scope mistake?',
			answer:
				'Using broader scope than necessary. That makes values easier to expose accidentally and weakens the access model.',
		},
	],
	'concepts/api-keys': [
		{
			question: 'Does an API key belong to one project?',
			answer:
				'Yes. An API key is bound to one project and can also be narrowed by environment and scope.',
		},
		{
			question: 'Should I create one key per app or service?',
			answer:
				'Usually yes. Separate runtimes should usually get separate keys so access stays narrower and easier to reason about.',
		},
		{
			question: 'Should frontend keys use client scope?',
			answer:
				'Yes, in most cases. Frontend and mobile apps should usually use client scope unless there is a real need for broader access.',
		},
		{
			question: 'What is the most common API key mistake?',
			answer:
				'Using keys that are broader than they need to be. That increases blast radius and makes accidental exposure harder to contain.',
		},
	],
	'concepts/history-and-rollback': [
		{
			question: 'When should I use rollback instead of editing the value manually?',
			answer:
				'Use rollback when you already know a previous version was good. That is safer than retyping a value during an incident.',
		},
		{
			question: 'What kind of changes show up in history?',
			answer:
				'History helps you inspect changes to the value and other important entry fields such as scope or content type.',
		},
		{
			question: 'Is rollback only for feature flags?',
			answer:
				'No. Rollback is useful for feature flags, kill switches, and broader runtime config values.',
		},
		{
			question: 'What is the biggest rollback mistake?',
			answer:
				'Guessing a replacement value instead of restoring a known good version. That slows incident response and increases the chance of a second mistake.',
		},
	],
	'concepts/audit-logs': [
		{
			question: 'What kinds of actions appear in audit logs?',
			answer:
				'The current repo wires audit logging around important admin actions such as project, user, environment, parameter, and share-link changes.',
		},
		{
			question: 'Are audit logs only for security reviews?',
			answer:
				'No. They are also part of normal operational workflows such as incident review, production change tracking, and collaboration across multiple operators.',
		},
		{
			question: 'Should I check audit logs after a rollback?',
			answer:
				'Often yes. Rollback handles the immediate recovery, but audit logs help you understand the timeline and the operator actions around the event.',
		},
		{
			question: 'Why do parameter share links matter in audit logs?',
			answer:
				'Because share-link creation and revocation are sensitive collaboration actions and should remain visible in the operational record.',
		},
	],
	'concepts/users-and-project-access': [
		{
			question: 'Does SSO bypass project access control?',
			answer:
				'No. SSO only changes how a user authenticates. Project access still determines what the user can see and edit afterward.',
		},
		{
			question: 'Should I invite users instead of sharing one admin account?',
			answer:
				'Yes. Invitation-based onboarding and per-user access are much safer than sharing one broad admin credential.',
		},
		{
			question: 'Can access be limited by project?',
			answer:
				'Yes. Project boundaries are part of the intended access-control model, especially when one Nona instance serves multiple apps or teams.',
		},
		{
			question: 'What is the safest first collaboration model?',
			answer:
				'Create the project structure first, invite users individually, then grant each person only the project access they actually need.',
		},
	],
	'deployment': [
		{
			question: 'What is the right first production deployment for most teams?',
			answer:
				'Standalone is usually the right first production deployment. It is simpler to operate and usually enough unless you already know you need a replica read topology.',
		},
		{
			question: 'When should I choose primary/replica instead of standalone?',
			answer:
				'Choose primary/replica only when read-heavy workloads justify the extra complexity and eventual consistency is acceptable for replica reads.',
		},
		{
			question: 'Is deployment part of the product story for Nona?',
			answer:
				'Yes. Because Nona is self-hosted, deployment is part of using the product, not a separate concern you can ignore.',
		},
		{
			question: 'What should I do right after the deployment is live?',
			answer:
				'Create the first admin account, create a project and environments, validate a real read path, and make sure backups are in place.',
		},
	],
	'deployment/standalone': [
		{
			question: 'Is standalone only for testing?',
			answer:
				'No. For many teams, standalone is not only the first production step. It remains the long-term deployment shape.',
		},
		{
			question: 'What is the most important thing to preserve in standalone mode?',
			answer:
				'The persistent data mounted at /var/lib/nona. That is the durable state you need to keep across restarts and upgrades.',
		},
		{
			question: 'Should I pin JWT settings in production?',
			answer:
				'Usually yes, if you want the deployment to be easier to reason about operationally.',
		},
		{
			question: 'When should I leave standalone and move to replica mode?',
			answer:
				'Only when you already know that read-heavy traffic and operational requirements justify the added complexity.',
		},
	],
	'deployment/primary-replica': [
		{
			question: 'Should I use primary/replica just because it sounds more production-like?',
			answer:
				'No. For many teams, standalone is still the better production choice.',
		},
		{
			question: 'What is the biggest tradeoff in primary/replica mode?',
			answer:
				'Eventual consistency on the replica read path. Writes on the primary may not be visible on the replica immediately.',
		},
		{
			question: 'Should admin and write traffic go to the replica?',
			answer:
				'No. Admin and write workflows should stay on the primary.',
		},
		{
			question: 'What should I validate after bringing up this topology?',
			answer:
				'Validate the primary admin and write path, the replica read path, the expected ports, and the replication relationship.',
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
			question: 'Can Nona do percentage rollout or beta-cohort targeting?',
			answer:
				'No. Nona does not evaluate flags against userId, request attributes, segments, cohorts, or percentage rules. The built-in model is direct environment-and-key lookup.',
		},
		{
			question: 'When is Nona a good fit for feature flags?',
			answer:
				'Nona is a strong fit when you want self-hosted, open source feature flags with simpler operations and one product for flags and runtime config.',
		},
	],
	'feature-flags/what-are-feature-flags': [
		{
			question: 'Are feature flags only for frontend releases?',
			answer:
				'No. Feature flags are useful for frontend, mobile, and backend behavior, which is why Nona documents all three use cases.',
		},
		{
			question: 'How do feature flags work in Nona?',
			answer:
				'In Nona, a feature flag is usually a boolean config entry. That keeps the model simple and aligned with the same project, environment, scope, and API key system as the rest of the product.',
		},
		{
			question: 'Are feature flags the same as remote config?',
			answer:
				'Not exactly. Feature flags are one important type of runtime config, but remote config is broader and also includes text, number, and JSON values.',
		},
		{
			question: 'What is the best first feature flag to create?',
			answer:
				'A simple boolean key such as Features:Checkout is usually the best first choice because it is easy to create, read, and flip.',
		},
		{
			question: 'Can I roll a flag out to 10 percent of users in Nona?',
			answer:
				'No. Nona does not provide built-in percentage rollout, beta-cohort targeting, or per-user evaluation. The built-in model is direct environment-and-key lookup.',
		},
	],
	'feature-flags/kill-switches': [
		{
			question: 'What makes a kill switch different from a normal feature flag?',
			answer:
				'A kill switch is a feature flag whose main job is fast disablement under real operational pressure.',
		},
		{
			question: 'Should a kill switch always be boolean?',
			answer:
				'Usually yes. Boolean values are the clearest fit for a fast on/off operational control.',
		},
		{
			question: 'What is the best first kill switch candidate?',
			answer:
				'A risky production path such as checkout, payments, onboarding, or a third-party integration is usually the best first candidate.',
		},
		{
			question: 'Why does rollback matter for kill switches?',
			answer:
				'Because incident changes happen fast, and rollback gives you a safer way to return to a known earlier state than retyping values manually.',
		},
	],
	'feature-flags/backend-feature-flags': [
		{
			question: 'Why are backend feature flags important?',
			answer:
				'They control behavior that the rest of the stack depends on, such as route gates, integrations, and operational toggles.',
		},
		{
			question: 'Should backend flags use server scope?',
			answer:
				'Usually yes. Backend-only flags should stay on server scope whenever possible.',
		},
		{
			question: 'Can backend flags work as kill switches?',
			answer:
				'Yes. Backend flags are often some of the highest-value kill switches in a system.',
		},
		{
			question: 'What is a good first backend flag?',
			answer:
				'A clear operational flag such as Features:DisablePayments or Features:UseLegacySearch is usually a strong first candidate.',
		},
	],
	'feature-flags/mobile-app-feature-flags': [
		{
			question: 'Why do mobile apps benefit so much from feature flags?',
			answer:
				'Because mobile release cycles are slower than web deploys, and flags let teams change behavior without waiting for another store release.',
		},
		{
			question: 'Should mobile flags use client scope?',
			answer:
				'Usually yes for values the app reads directly. Keep sensitive decisions on the server when possible.',
		},
		{
			question: 'What is a good first mobile feature flag?',
			answer:
				'A flag such as Features:UseNewOnboarding or Features:Checkout is usually a good first test because the behavior is easy to see.',
		},
		{
			question: 'Can mobile feature flags also work as kill switches?',
			answer:
				'Yes. That is one of the strongest uses of flags in mobile applications.',
		},
	],
	'feature-flags/feature-flags-vs-remote-config': [
		{
			question: 'Are feature flags and remote config the same thing?',
			answer:
				'No. Feature flags are usually on or off runtime switches, while remote config is the broader category for runtime values that can also be text, number, or JSON.',
		},
		{
			question: 'Why does Nona use one system for both?',
			answer:
				'Because many teams need both behavior toggles and broader runtime settings, and one shared model is easier to operate than multiple separate tools.',
		},
		{
			question: 'When should I start with a feature flag?',
			answer:
				'Start with a feature flag when the question is fundamentally on or off, such as enabling a flow or adding a kill switch.',
		},
		{
			question: 'When should I start with remote config instead?',
			answer:
				'Start with remote config when the value is a threshold, text string, JSON object, or another non-boolean setting that should change at runtime.',
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
	'migration/firebase-concept-mapping': [
		{
			question: 'Does Firebase map one-to-one into Nona?',
			answer:
				'No. The migration translates from Firebase source concepts into Nona target concepts rather than preserving a one-to-one product model.',
		},
		{
			question: 'Do Firebase conditions stay as runtime targeting rules?',
			answer:
				'No. They are mapped into explicit Nona environments during migration instead of remaining a Firebase-style runtime rules engine.',
		},
		{
			question: 'Do Firebase boolean values stay useful after migration?',
			answer:
				'Yes. Boolean Firebase values map naturally into Nona boolean entries, which means they continue to work well as feature flags.',
		},
		{
			question: 'What is the biggest concept shift to understand before migrating?',
			answer:
				'The biggest shift is that Nona is a self-hosted project, environment, and scope model, not Firebase with renamed screens.',
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
	'remote-config/what-is-remote-config': [
		{
			question: 'Is remote config only for feature flags?',
			answer:
				'No. Feature flags are one remote-config use case, but remote config also includes text, number, and JSON values that change runtime behavior.',
		},
		{
			question: 'What is the main benefit of remote config?',
			answer:
				'The main benefit is changing application behavior without shipping a new build for every small adjustment.',
		},
		{
			question: 'How does Nona make remote config concrete?',
			answer:
				'Nona turns it into an explicit model of projects, environments, typed entries, scopes, and API keys instead of treating it as a vague dynamic-settings layer.',
		},
		{
			question: 'What is a good first remote-config value?',
			answer:
				'A simple value such as App:BannerText or Limits:MaxItems is usually a good first step because it is easy to create and verify.',
		},
	],
	'remote-config/server-side-remote-config': [
		{
			question: 'What is server-side remote config?',
			answer:
				'It means backend services read runtime values from a configuration service instead of hardcoding everything into deploy-time settings.',
		},
		{
			question: 'Should backend remote config use server scope?',
			answer:
				'Usually yes. Backend-only values should stay on server scope whenever possible.',
		},
		{
			question: 'What is a good first server-side remote-config value?',
			answer:
				'A threshold such as Limits:MaxItems or a boolean flag such as Features:UseLegacySearch is usually a strong first example.',
		},
		{
			question: 'Why is Nona a good fit for server-side remote config?',
			answer:
				'Because it is self-hosted, plain HTTP accessible, and designed to separate server-only values clearly.',
		},
	],
	'remote-config/remote-config-vs-environment-variables': [
		{
			question: 'Are environment variables and remote config competing systems?',
			answer:
				'No. Most teams use both. The question is which values belong in which layer.',
		},
		{
			question: 'What should stay in environment variables?',
			answer:
				'Keep deployment wiring, secret material, and infrastructure-specific settings in environment variables.',
		},
		{
			question: 'What should move into remote config?',
			answer:
				'Move values that should change after deployment, such as feature flags, copy, thresholds, and runtime behavior settings.',
		},
		{
			question: 'What is a good first split to implement?',
			answer:
				'Keep the Nona API key in environment variables, then move one runtime value such as App:BannerText into Nona.',
		},
	],
	'remote-config/use-cases': [
		{
			question: 'What are the most common Nona remote-config use cases?',
			answer:
				'Kill switches, mobile app settings, backend thresholds, environment-specific values, and structured JSON settings are some of the most common.',
		},
		{
			question: 'Can Nona handle both feature flags and broader settings?',
			answer:
				'Yes. That is one of the product main strengths: one system for boolean flags and non-boolean runtime values.',
		},
		{
			question: 'What is the best first use case to implement?',
			answer:
				'A simple kill switch or one runtime text value is usually the easiest first use case to validate.',
		},
		{
			question: 'Why do these use cases matter for docs SEO?',
			answer:
				'Because they map the product model to concrete operator and developer problems instead of only describing the tool abstractly.',
		},
	],
	'remote-config/mobile-app-remote-config': [
		{
			question: 'Why do mobile apps need remote config?',
			answer:
				'Because mobile release cycles are slower than web deploys, and remote config lets teams change values after the app has already shipped.',
		},
		{
			question: 'Is mobile remote config only about feature flags?',
			answer:
				'No. Mobile apps often need both feature flags and broader runtime values such as copy, thresholds, supported versions, and grouped settings.',
		},
		{
			question: 'Should mobile remote-config values use client scope?',
			answer:
				'Usually yes for values the app reads directly. Keep sensitive or backend-only decisions on server scope where possible.',
		},
		{
			question: 'What is a good first mobile remote-config value?',
			answer:
				'App:BannerText or App:MinimumSupportedVersion is usually a good first value because it is easy to create and observe in the app.',
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
				'Create the first account with nona auth register or the admin UI, create a project and environment, then add a parameter and test a real read.',
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
	'get-started': [
		{
			question: 'What is the shortest path to a working Nona setup?',
			answer:
				'Deploy the container, create one project, add one boolean parameter, create one API key, and verify one read.',
		},
		{
			question: 'Do I need to understand the whole product before starting?',
			answer:
				'No. The get-started path is designed to teach the core model while you are using it.',
		},
		{
			question: 'Should I start with feature flags or remote config first?',
			answer:
				'Either is fine, but many teams start with one boolean flag because it is the easiest thing to verify quickly.',
		},
		{
			question: 'What should I read after the first successful setup?',
			answer:
				'Most teams continue into feature flags, remote config, migration, or deployment depending on what they are trying to do next.',
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
	'operations': [
		{
			question: 'When should I start reading the operations docs?',
			answer:
				'Start when you are preparing a real production deployment or when more than one person will operate the instance.',
		},
		{
			question: 'Are operations docs only about infrastructure?',
			answer:
				'No. They also cover operator workflows such as admin access, backups, upgrades, and reviewing operational history.',
		},
		{
			question: 'What should I secure first in a production setup?',
			answer:
				'Start with admin access and authentication, then make sure backups are in place before treating the instance as production-ready.',
		},
		{
			question: 'Why are operations part of the product story for Nona?',
			answer:
				'Because Nona is self-hosted. Operating the service is part of using the product correctly, not a separate concern you can ignore.',
		},
	],
	'operations/sso': [
		{
			question: 'Does SSO replace API keys?',
			answer:
				'No. SSO is for admin authentication and onboarding. Runtime config reads still use API keys.',
		},
		{
			question: 'Which SSO providers does Nona support?',
			answer:
				'The current repo supports Google SSO and Microsoft SSO.',
		},
		{
			question: 'Can any valid Google or Microsoft account sign in?',
			answer:
				'No. The SSO identity still has to match a Nona user account, and invitation-based onboarding keeps that linking explicit and safer.',
		},
		{
			question: 'Should Microsoft SSO stay on the common tenant in production?',
			answer:
				'Only if that is what your organization actually wants. If you want to restrict sign-in to one tenant, set a specific tenant id instead.',
		},
	],
	'operations/security-and-authentication': [
		{
			question: 'Does SSO replace API keys for runtime reads?',
			answer:
				'No. SSO is for admin access. Runtime config consumers still authenticate with API keys.',
		},
		{
			question: 'What should I lock down first in production?',
			answer:
				'Start with admin access, narrow API keys, limited project access, and stable JWT settings if you pin them.',
		},
		{
			question: 'Should teams share one broad admin account?',
			answer:
				'No. One account per operator is a safer and more auditable operating model.',
		},
		{
			question: 'Are share links a replacement for user access?',
			answer:
				'No. Share links are useful for narrow temporary collaboration, but they are not a replacement for normal user and project access control.',
		},
	],
	'operations/backups': [
		{
			question: 'What is the most important thing to back up?',
			answer:
				'The persistent data mounted at /var/lib/nona. That is where the durable Nona state lives in the documented Docker deployment paths.',
		},
		{
			question: 'Is backing up the container image enough?',
			answer:
				'No. The container image is not the durable application state. The mounted persistent data is what matters.',
		},
		{
			question: 'When should I take a backup?',
			answer:
				'Take one before upgrades, topology changes, storage work, host replacement, or other risky maintenance that could affect persistent state.',
		},
		{
			question: 'How do I know whether a backup is actually useful?',
			answer:
				'You know it is useful when you can restore it and validate that the service starts, login works, and a known config read still succeeds.',
		},
	],
	'operations/upgrades': [
		{
			question: 'What is the safest first step before an upgrade?',
			answer:
				'Take a backup first. That gives you a recovery path before you touch the running deployment.',
		},
		{
			question: 'What should stay stable during an upgrade?',
			answer:
				'The persistent data volumes and any pinned JWT settings should stay stable across the upgrade.',
		},
		{
			question: 'How should I verify the upgrade worked?',
			answer:
				'Check that the service starts, login still works, and a known config read still succeeds.',
		},
		{
			question: 'Is a quick UI check enough after an upgrade?',
			answer:
				'No. A real validation read is much more reliable than assuming the upgrade worked because the UI loads.',
		},
	],
	'parameter-share-links': [
		{
			question: 'What makes parameter share links different from normal user access?',
			answer:
				'They provide narrow, temporary access to one parameter instead of broader ongoing project access.',
		},
		{
			question: 'Should I prefer view-only links by default?',
			answer:
				'Yes. Use view-only unless the other person truly needs edit access to that one parameter.',
		},
		{
			question: 'Are share-link tokens sensitive?',
			answer:
				'Yes. Anyone with the token can use the public share-link endpoint until the link expires or is revoked, so treat the token as a secret.',
		},
		{
			question: 'When should I use a share link instead of inviting a user?',
			answer:
				'Use a share link when the access need is temporary, narrow, and limited to one parameter. Use normal user or project access for ongoing collaboration.',
		},
	],
	'why-nona': [
		{
			question: 'Why do teams choose Nona instead of a hosted control plane?',
			answer:
				'Usually because they want runtime control, self-hosting, open source visibility, and a smaller product model that they can operate directly.',
		},
		{
			question: 'Is Nona only for remote config?',
			answer:
				'No. Nona supports feature flags and broader remote config in the same system.',
		},
		{
			question: 'What is the fastest way to evaluate Nona?',
			answer:
				'Run the Docker image, create one project, add one boolean flag and one text value, then read them over HTTP.',
		},
		{
			question: 'What kind of team is Nona best for?',
			answer:
				'Teams that want self-hosted feature flags and remote config, plain HTTP access, and a Docker-first operating model are usually a strong fit.',
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
			name: 'Create the first admin account',
			text: 'Run nona auth register against http://localhost:18080 for non-interactive setup, or visit http://localhost:18080/register in the admin UI.',
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
	'clients/http': [
		{
			name: 'Prepare the value and key',
			text: 'Create or confirm the target parameter and an API key with the correct scope and environment.',
		},
		{
			name: 'Encode the key path',
			text: 'URL-encode the key name before building the request URL.',
		},
		{
			name: 'Send the HTTP request',
			text: 'Make the request with the X-Api-Key header against the running Nona instance.',
		},
		{
			name: 'Inspect the response',
			text: 'Read the raw body and inspect the X-Nona-Content-Type header to understand the logical value type.',
		},
		{
			name: 'Troubleshoot if needed',
			text: 'If the request fails, check the environment, key, URL encoding, project binding, and scope alignment.',
		},
	],
	'clients/javascript': [
		{
			name: 'Install the package',
			text: 'Install nona-client in the application runtime.',
		},
		{
			name: 'Prepare the parameter and key',
			text: 'Create one parameter and one API key so the app has a real value to read.',
		},
		{
			name: 'Configure the client',
			text: 'Set baseUrl, environmentId, and apiKey when creating the Nona client instance.',
		},
		{
			name: 'Read one value',
			text: 'Use getConfigValue or a typed helper to read one real value from the Nona instance.',
		},
		{
			name: 'Verify a real change',
			text: 'Edit the value in Nona and confirm the application sees the update before adding more abstraction or cache.',
		},
	],
	'clients/dotnet': [
		{
			name: 'Install the package',
			text: 'Install Nona.Client in the .NET application.',
		},
		{
			name: 'Prepare the parameter and key',
			text: 'Create one parameter and one API key with the correct backend scope.',
		},
		{
			name: 'Configure the client',
			text: 'Set BaseAddress, EnvironmentId, and ApiKey in the NonaClient or NonaClientOptions.',
		},
		{
			name: 'Read one value',
			text: 'Use GetConfigValueAsync or a typed helper to read one real value from Nona.',
		},
		{
			name: 'Verify the service sees updates',
			text: 'Change the value in Nona and confirm the service reads the expected result before tuning cache behavior.',
		},
	],
	'clients/openfeature': [
		{
			name: 'Prepare a boolean flag and key',
			text: 'Create one boolean parameter and an API key with the correct scope so the provider has a real flag to resolve.',
		},
		{
			name: 'Install the provider package',
			text: 'Install the Nona OpenFeature provider package for the runtime you are using.',
		},
		{
			name: 'Register the provider',
			text: 'Configure the provider with the Nona base URL, API key, and environment or client instance depending on the runtime.',
		},
		{
			name: 'Resolve one flag',
			text: 'Read one boolean flag through OpenFeature so the abstraction is tested end to end.',
		},
		{
			name: 'Verify the flag changes',
			text: 'Edit the value in Nona and confirm the application sees the updated flag through OpenFeature.',
		},
	],
	'deployment/standalone': [
		{
			name: 'Start the service',
			text: 'Run the standalone container or compose file so one Nona instance is live.',
		},
		{
			name: 'Mount persistent storage',
			text: 'Keep a persistent volume mounted at /var/lib/nona so the service retains state across restarts and upgrades.',
		},
		{
			name: 'Pin JWT settings if needed',
			text: 'Provide stable JWT settings if that is part of your production operating model.',
		},
		{
			name: 'Validate the service',
			text: 'Confirm the admin UI and API respond and that one real config read succeeds.',
		},
		{
			name: 'Protect the deployment',
			text: 'Set up backups before you rely on the standalone instance operationally.',
		},
	],
	'deployment/primary-replica': [
		{
			name: 'Start the topology',
			text: 'Bring up the compose deployment so both primary and replica services are running.',
		},
		{
			name: 'Preserve persistent storage',
			text: 'Keep persistent data for both the primary and replica services.',
		},
		{
			name: 'Keep JWT settings aligned',
			text: 'Use the same pinned JWT settings on both services if you pin them at all.',
		},
		{
			name: 'Use the correct traffic paths',
			text: 'Keep admin and write traffic on the primary and use the replica only for read-heavy paths that tolerate eventual consistency.',
		},
		{
			name: 'Validate replication behavior',
			text: 'Confirm the primary and replica endpoints behave as expected and that replica reads match your consistency expectations.',
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
			item: createAbsoluteUrl(`/docs/${docsSegments[0]}`),
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
		{ name: 'Get Started', path: '/docs/get-started' },
		{ name: 'Feature Flags', path: '/docs/feature-flags' },
		{ name: 'Remote Config', path: '/docs/remote-config' },
		{ name: 'Core Concepts', path: '/docs/concepts' },
		{ name: 'Clients and API', path: '/docs/clients' },
		{ name: 'Deployment', path: '/docs/deployment' },
		{ name: 'Operations', path: '/docs/operations' },
		{ name: 'Migration', path: '/docs/migration' },
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

	route.head.push(
		{ tag: 'meta', attrs: { property: 'og:image', content: OG_IMAGE } },
		{ tag: 'meta', attrs: { property: 'og:image:width', content: '1200' } },
		{ tag: 'meta', attrs: { property: 'og:image:height', content: '630' } },
		{ tag: 'meta', attrs: { property: 'og:image:alt', content: OG_IMAGE_ALT } },
		{ tag: 'meta', attrs: { name: 'twitter:image', content: OG_IMAGE } },
	);

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
