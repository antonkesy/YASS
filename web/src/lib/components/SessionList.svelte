<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount, formatTime, sessionStateLabel } from '$lib/format';
	import { resolve } from '$app/paths';
	import { Icon } from '$lib/icons';

	let { sessions }: { sessions: SessionDto[] } = $props();

	function tone(state: SessionState) {
		switch (state) {
			case SessionState.Scanning:
			case SessionState.Exporting:
				return 'text-accent-deep';
			case SessionState.Failed:
				return 'text-danger';
			case SessionState.Done:
				return 'text-saved-ink';
			default:
				return 'text-ink-muted';
		}
	}
</script>

{#if sessions.length === 0}
	<div
		class="flex animate-fade-in flex-col items-center gap-2 rounded-[18px] bg-app px-3 py-9 text-center"
		data-testid="no-sessions"
	>
		<span
			class="flex h-11 w-11 items-center justify-center rounded-[14px] bg-surface text-accent shadow-chip"
		>
			<Icon name="archive" />
		</span>
		<p class="text-[15px] font-extrabold">No past scans yet</p>
		<p class="text-[13px] text-balance text-ink-muted">Every scan is saved here automatically.</p>
	</div>
{:else}
	<ul class="flex flex-col gap-1" data-testid="session-list">
		{#each sessions as session, i (session.Id)}
			<li class="animate-page-in" style="animation-delay: {Math.min(i, 10) * 45}ms">
				<a
					href={resolve('/session/[id]', { id: session.Id })}
					class="flex min-h-14 items-center justify-between gap-3 rounded-[14px] px-2.5 py-2 transition-colors hover:bg-accent-hover50"
				>
					<span class="flex flex-col">
						<span class="text-sm font-bold">{formatTime(session.CreatedAt)}</span>
						<span class="text-xs font-semibold {tone(session.State)}">
							{sessionStateLabel(session.State)}
						</span>
					</span>
					<span class="text-xs text-ink-muted">
						{activePageCount(session)} / {session.Pages.length}
						{session.Pages.length === 1 ? 'page' : 'pages'}
					</span>
				</a>
			</li>
		{/each}
	</ul>
{/if}
