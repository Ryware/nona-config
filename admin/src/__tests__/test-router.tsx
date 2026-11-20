import { createRouter, type RouteDefinition } from "@solidjs/router";
import type { Component, ParentProps } from "solid-js";
import type { JSX } from "@solidjs/web";

/** Route declarations used by integration tests are converted to Router 2 config. */
export function Route(props: RouteDefinition): JSX.Element {
  return props as unknown as JSX.Element;
}

function routeDefinitions(node: unknown): RouteDefinition[] {
  const value = typeof node === "function" ? node() : node;
  const entries = Array.isArray(value) ? value : [value];
  return entries.map(entry => {
    const route = (typeof entry === "function" ? entry() : entry) as RouteDefinition & { children?: unknown };
    return route.children
      ? { ...route, children: routeDefinitions(route.children) }
      : route;
  });
}

export function Router(props: ParentProps<{ root?: Component<ParentProps> }>): JSX.Element {
  // Test declarations are static route configuration, evaluated once at mount.
  // eslint-disable-next-line solid/reactivity
  const initialChildren = props.children;
  const routes = routeDefinitions(initialChildren);
  const TestRouter = createRouter({ routes });
  // eslint-disable-next-line solid/reactivity
  const Root = props.root;
  return <TestRouter>{route => Root ? <Root>{route.children}</Root> : route.children}</TestRouter>;
}

export function MetaProvider(props: ParentProps): JSX.Element {
  return <>{props.children}</>;
}
