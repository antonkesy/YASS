<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		roots,
		recent,
		onpick,
		onclose
	}: {
		/** Folders the server will accept, from output.allowed_roots. */
		roots: string[];
		/** Folders used before, newest first. */
		recent: string[];
		onpick: (directory: string) => void;
		onclose: () => void;
	} = $props();

	let custom = $state('');

	// Recent first, then the roots, without repeating anything. Six is the mockup's cap.
	const destinations = $derived.by(() => {
		const out: { path: string; hint: string; recent: boolean }[] = [];
		for (const path of [...recent, ...roots]) {
			const clean = path.trim().replace(/\/+$/, '');
			if (clean === '' || out.some((d) => d.path === clean)) continue;
			out.push({
				path: clean,
				hint: recent.includes(path) ? 'Recent' : 'Allowed folder',
				recent: recent.includes(path)
			});
			if (out.length === 6) break;
		}
		return out;
	});
</script>

<button type="button" class="fixed inset-0 z-34 cursor-default" aria-label="Close" onclick={onclose}
></button>

<div
	class="absolute top-[calc(100%+8px)] right-0 left-0 z-35 flex animate-pop-in flex-col gap-0.5 rounded-[20px] bg-surface p-2 shadow-popover"
	role="dialog"
	aria-label="Save to a folder"
	data-testid="save-to-menu"
>
	<p class="px-2.5 py-1.5 text-[13px] font-extrabold">Save to folder on the server</p>

	{#each destinations as destination, i (destination.path)}
		<button
			type="button"
			class="flex min-h-11 animate-page-in items-center gap-2.5 rounded-[12px] px-2.5 text-left transition-colors hover:bg-accent-hover50"
			style="animation-delay: {i * 40}ms"
			onclick={() => onpick(destination.path)}
		>
			<span
				class="flex h-8 w-8 flex-none items-center justify-center rounded-[10px] {destination.recent
					? 'bg-saved-bg text-saved-ink'
					: 'bg-accent-50 text-accent'}"
			>
				<Icon name={destination.recent ? 'folderOpen' : 'folder'} />
			</span>
			<span class="flex min-w-0 flex-col">
				<span class="truncate font-mono text-[13px] font-bold">{destination.path}</span>
				<span class="text-[11px] text-ink-muted">{destination.hint}</span>
			</span>
		</button>
	{/each}

	<div class="mt-1 flex gap-2 border-t border-line pt-2">
		<input
			class="min-w-0 flex-1 rounded-[10px] border-[1.5px] border-line-strong bg-transparent px-2.5 font-mono text-[13px] outline-none focus:border-accent"
			type="text"
			placeholder="/other/path"
			spellcheck="false"
			bind:value={custom}
			data-testid="save-to-custom"
		/>
		<button
			type="button"
			class="h-[38px] rounded-[10px] bg-accent px-3 text-[13px] font-bold text-white transition-opacity disabled:pointer-events-none disabled:opacity-45"
			disabled={custom.trim() === ''}
			onclick={() => onpick(custom.trim())}
		>
			Save here
		</button>
	</div>
</div>
