import { Title } from "@solidjs/meta";
import { useQuery } from "@tanstack/solid-query";
import { Show } from "solid-js";
import { apiClient } from "../../shared/api/client";
import { MIcon } from "../../shared/ui/icons";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import type { DashboardCounts } from "../../types";

function fetchDashboardCounts(): Promise<DashboardCounts> {
  return apiClient.get<DashboardCounts>("/admin/dashboard/counts");
}

interface StatCardProps {
  label: string;
  icon: string;
  value: number | undefined;
  isLoading: boolean;
  testId?: string;
}

function StatCard(props: StatCardProps) {
  return (
    <div
      data-testid={props.testId}
      class="bg-surface-container-low border-outline-variant/15 flex flex-col gap-4 rounded-2xl border p-6 shadow-sm"
    >
      <div class="flex items-center justify-between">
        <span class="text-on-surface-variant text-[11px] font-medium tracking-[0.05em] uppercase">
          {props.label}
        </span>
        <span class="material-symbols-outlined text-primary text-[20px]">{props.icon}</span>
      </div>
      <div class="font-headline text-on-surface text-[36px] leading-none font-bold">
        <Show
          when={!props.isLoading}
          fallback={<div class="bg-surface-container-highest h-9 w-16 animate-pulse rounded-lg" />}
        >
          {props.value?.toLocaleString() ?? "—"}
        </Show>
      </div>
    </div>
  );
}

export default function DashboardPage() {
  const countsQuery = useQuery(() => ({
    queryKey: ["dashboard-counts"],
    queryFn: fetchDashboardCounts
  }));

  return (
    <>
      <Title>Dashboard | Nona Config Admin</Title>
      <div class="animate-page-enter space-y-6">
        <div class="space-y-1.5">
          <h2
            data-testid="dashboard-heading"
            class="font-headline text-on-surface text-[17px] font-bold tracking-tight"
          >
            Dashboard
          </h2>
          <p class="text-on-surface-variant text-[12.5px]">System overview at a glance</p>
        </div>

        <Show when={countsQuery.isError}>
          <QueryErrorBanner
            message="Failed to load dashboard stats."
            onRetry={() => countsQuery.refetch()}
          />
        </Show>

        <div class="animate-stagger grid grid-cols-1 gap-4 sm:grid-cols-3">
          <StatCard
            label="Projects"
            icon="folder"
            value={countsQuery.status === "success" ? countsQuery.data?.projects : undefined}
            isLoading={countsQuery.isLoading}
            testId="dashboard-stat-projects"
          />
          <StatCard
            label="Config Entries"
            icon="settings"
            value={countsQuery.status === "success" ? countsQuery.data?.configEntries : undefined}
            isLoading={countsQuery.isLoading}
            testId="dashboard-stat-config-entries"
          />
          <StatCard
            label="Team Members"
            icon="group"
            value={countsQuery.status === "success" ? countsQuery.data?.users : undefined}
            isLoading={countsQuery.isLoading}
            testId="dashboard-stat-team-members"
          />
        </div>

        {/* Quick Actions */}
        <div class="space-y-3">
          <h3 class="font-headline text-on-surface/90 text-[14px] font-bold">Quick Actions</h3>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <a
              data-testid="dashboard-manage-projects-link"
              href="/projects"
              class="bg-surface-container-low border-outline-variant/15 hover:bg-surface-container-high hover:border-primary/30 group flex cursor-pointer items-center gap-4 rounded-2xl border p-5 text-current no-underline transition-all"
            >
              <div class="bg-primary/10 text-primary flex h-10 w-10 items-center justify-center rounded-xl transition-transform group-hover:scale-110">
                <MIcon name="folder" class="text-[20px]" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="font-headline text-on-surface group-hover:text-primary text-[14px] font-semibold transition-colors">
                  Manage Projects
                </div>
                <div class="text-outline mt-0.5 text-[11px]">
                  Configure environments and parameters
                </div>
              </div>
              <MIcon
                name="chevron_right"
                class="text-outline text-lg transition-transform group-hover:translate-x-0.5"
              />
            </a>

            <a
              data-testid="dashboard-team-management-link"
              href="/users"
              class="bg-surface-container-low border-outline-variant/15 hover:bg-surface-container-high hover:border-primary/30 group flex cursor-pointer items-center gap-4 rounded-2xl border p-5 text-current no-underline transition-all"
            >
              <div class="bg-primary/10 text-primary flex h-10 w-10 items-center justify-center rounded-xl transition-transform group-hover:scale-110">
                <MIcon name="group" class="text-[20px]" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="font-headline text-on-surface group-hover:text-primary text-[14px] font-semibold transition-colors">
                  Team Management
                </div>
                <div class="text-outline mt-0.5 text-[11px]">Manage members, roles, and access</div>
              </div>
              <MIcon
                name="chevron_right"
                class="text-outline text-lg transition-transform group-hover:translate-x-0.5"
              />
            </a>

            <a
              data-testid="dashboard-audit-logs-link"
              href="/audit-logs"
              class="bg-surface-container-low border-outline-variant/15 hover:bg-surface-container-high hover:border-primary/30 group flex cursor-pointer items-center gap-4 rounded-2xl border p-5 text-current no-underline transition-all"
            >
              <div class="bg-primary/10 text-primary flex h-10 w-10 items-center justify-center rounded-xl transition-transform group-hover:scale-110">
                <MIcon name="manage_history" class="text-[20px]" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="font-headline text-on-surface group-hover:text-primary text-[14px] font-semibold transition-colors">
                  Security Audit Logs
                </div>
                <div class="text-outline mt-0.5 text-[11px]">Audit changes and user activities</div>
              </div>
              <MIcon
                name="chevron_right"
                class="text-outline text-lg transition-transform group-hover:translate-x-0.5"
              />
            </a>
          </div>
        </div>
      </div>
    </>
  );
}
