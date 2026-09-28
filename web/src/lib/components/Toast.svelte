<script lang="ts">
	import { toast } from '$lib/toast';
</script>

{#if $toast}
	{#key $toast.id}
		<div
			class="fixed bottom-6 left-1/2 z-60 flex -translate-x-1/2 animate-toast-in items-center gap-3 rounded-full bg-ink px-[18px] py-3 text-sm font-semibold text-white shadow-toast"
			role="status"
			aria-live="polite"
			data-testid="toast"
		>
			{$toast.message}
			{#if $toast.undo}
				<button
					type="button"
					class="text-sm font-extrabold text-toast-undo"
					onclick={() => {
						$toast?.undo?.();
						toast.dismiss();
					}}
					data-testid="toast-undo"
				>
					Undo
				</button>
			{/if}
		</div>
	{/key}
{/if}
