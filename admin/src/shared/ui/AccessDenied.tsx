import { Title } from "@solidjs/meta";
import { MIcon } from "./icons";

export function AccessDenied() {
  return (
    <>
      <Title>Access Denied | Nona Config Admin</Title>
      <section
        role="alert"
        data-testid="access-denied"
        class="bg-surface-container-low border-outline-variant/15 mx-auto max-w-xl rounded-2xl border p-10 text-center"
      >
        <MIcon name="lock" class="text-primary mb-3 block text-4xl" />
        <h1 class="font-headline text-on-surface text-lg font-bold">Access denied</h1>
        <p class="text-on-surface-variant mt-2 text-[15px]">
          Ask an Admin if you need access to this page.
        </p>
      </section>
    </>
  );
}
