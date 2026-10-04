<script lang="ts">
	/**
	 * The header's one control on the right: a button that opens a small menu holding the
	 * scanner's status and the links out to the project. Modelled on SortMenu.
	 */
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import type { ConnectionState } from '$lib/hub/client';
	import StatusPill from './StatusPill.svelte';
	import { Icon } from '$lib/icons';

	let {
		status,
		connection,
		pages = null
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		/** Pages scanned so far while a scan runs; the status row counts them. */
		pages?: number | null;
	} = $props();

	const REPO = 'https://github.com/antonkesy/YAWSS';

	const links = [
		{ href: `${REPO}/issues/new`, label: 'Report an issue' },
		{ href: `${REPO}#readme`, label: 'Documentation' }
	];

	let open = $state(false);

	function keydown(e: KeyboardEvent) {
		if (open && e.key === 'Escape') {
			open = false;
			e.preventDefault();
		}
	}

	const item =
		'flex h-10 w-full items-center gap-2.5 rounded-[10px] px-2.5 text-left text-sm font-semibold text-ink-2 transition-colors hover:bg-accent-hover50 hover:text-ink';
</script>

<svelte:window onkeydown={keydown} />

<div class="relative">
	<button
		type="button"
		class="flex h-11 w-11 items-center justify-center rounded-[12px] text-ink shadow-chip transition-colors {open
			? 'bg-accent-wash'
			: 'bg-surface hover:bg-accent-wash'}"
		aria-label="Scanner status and help"
		aria-expanded={open}
		aria-haspopup="menu"
		onclick={() => (open = !open)}
		data-testid="app-menu-button"
	>
		<Icon name="more" />
	</button>

	{#if open}
		<!-- Click-catcher so the menu closes on any outside press, including a tap. -->
		<button
			type="button"
			class="fixed inset-0 z-30 cursor-default"
			aria-label="Close menu"
			onclick={() => (open = false)}
		></button>
		<div
			class="absolute top-[calc(100%+6px)] right-0 z-31 min-w-[230px] animate-pop-in rounded-[16px] bg-surface p-1.5 shadow-menu"
			role="menu"
			data-testid="app-menu"
		>
			<p class="px-2.5 py-1.5 text-[11px] font-bold tracking-[0.06em] text-ink-faint uppercase">
				Scanner
			</p>
			<div class="px-1 pb-1.5">
				<StatusPill {status} {connection} {pages} />
			</div>

			<hr class="mx-1 my-1 border-t border-line-strong" />

			<a
				href={REPO}
				target="_blank"
				rel="noreferrer noopener external"
				role="menuitem"
				class={item}
				onclick={() => (open = false)}
			>
				<!--
					Inlined rather than added to $lib/icons: that set is stroke-only (fill="none"),
					and a brand mark is a filled path.
				-->
				<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
					<path
						d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-2.91-.88-2.91-2.81 0-.82.29-1.49.77-2.01-.1-.21-.34-.98.07-2.04 0 0 .63-.2 2.06.77a6.9 6.9 0 0 1 1.87-.25c.63 0 1.27.08 1.87.25 1.43-.97 2.06-.77 2.06-.77.41 1.06.17 1.83.07 2.04.48.52.77 1.19.77 2.01 0 1.94-1.14 2.61-2.92 2.81.34.29.63.86.63 1.74 0 1.02-.01 1.85-.01 2.1 0 .21.15.46.55.38A7.99 7.99 0 0 0 16 8c0-4.42-3.58-8-8-8Z"
					/>
				</svg>
				GitHub
			</a>
			{#each links as link (link.href)}
				<a
					href={link.href}
					target="_blank"
					rel="noreferrer noopener external"
					role="menuitem"
					class={item}
					onclick={() => (open = false)}
				>
					{link.label}
				</a>
			{/each}
		</div>
	{/if}
</div>
