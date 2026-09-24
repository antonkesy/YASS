<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yawss.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yawss.Core.Domain';
	import { activePageCount, formatTime, sessionStateLabel } from '$lib/format';
	import { resolve } from '$app/paths';

	let { sessions }: { sessions: SessionDto[] } = $props();

	function tone(state: SessionState) {
		switch (state) {
			case SessionState.Scanning:
			case SessionState.Exporting:
				return 'text-amber-700';
			case SessionState.Failed:
				return 'text-rose-700';
			case SessionState.Done:
				return 'text-stone-500';
			default:
				return 'text-teal-700';
		}
	}
</script>

{#if sessions.length === 0}
	<p class="text-center text-stone-500" data-testid="no-sessions">No scans yet.</p>
{:else}
	<ul
		class="divide-y divide-stone-200 rounded-xl border border-stone-200 bg-white"
		data-testid="session-list"
	>
		{#each sessions as session (session.Id)}
			<li>
				<a
					href={resolve('/session/[id]', { id: session.Id })}
					class="flex min-h-14 items-center justify-between gap-3 px-4 py-3 hover:bg-stone-50 focus-visible:bg-stone-50 focus-visible:outline-none"
				>
					<span class="flex flex-col">
						<span class="font-medium">{formatTime(session.CreatedAt)}</span>
						<span class="text-sm {tone(session.State)}">{sessionStateLabel(session.State)}</span>
					</span>
					<span class="text-sm text-stone-600">
						{activePageCount(session)} / {session.Pages.length}
						{session.Pages.length === 1 ? 'page' : 'pages'}
					</span>
				</a>
			</li>
		{/each}
	</ul>
{/if}
