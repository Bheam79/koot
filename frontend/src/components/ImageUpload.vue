<script setup lang="ts">
// Small uploader for cover-image / question-image fields. Posts to /api/uploads/image
// and emits an "update:modelValue" with the server-relative URL.
import { computed, ref } from 'vue'
import { absoluteUrl, uploadImage } from '../api/quizzes'
import { useToast } from '../composables/useToast'

const props = defineProps<{
  modelValue?: string | null
  label?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', url: string | null): void
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const errorMsg = ref<string | null>(null)
const toast = useToast()

const previewUrl = computed(() => absoluteUrl(props.modelValue))

function trigger() {
  fileInput.value?.click()
}

async function onChange(ev: Event) {
  const input = ev.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  // Reset the input so re-uploading the same file fires change.
  input.value = ''

  if (file.size > 5 * 1024 * 1024) {
    const msg = 'Image must be 5 MB or smaller.'
    errorMsg.value = msg
    toast.error(msg)
    return
  }
  if (!/^image\/(jpe?g|png|gif|webp)$/i.test(file.type)) {
    const msg = 'Only JPG, PNG, GIF, or WEBP images are supported.'
    errorMsg.value = msg
    toast.error(msg)
    return
  }

  errorMsg.value = null
  uploading.value = true
  try {
    const res = await uploadImage(file)
    emit('update:modelValue', res.url)
  } catch (e: unknown) {
    const err = e as { response?: { data?: { error?: string } }; message?: string }
    const msg = err.response?.data?.error ?? err.message ?? 'Image upload failed. Please try again.'
    errorMsg.value = msg
    toast.error(msg)
  } finally {
    uploading.value = false
  }
}

function clearImage() {
  emit('update:modelValue', null)
  errorMsg.value = null
}
</script>

<template>
  <div>
    <p v-if="label" class="block text-sm font-medium text-slate-700 mb-1">{{ label }}</p>

    <div class="flex items-start gap-3">
      <div
        class="w-32 h-24 rounded-md border border-dashed border-slate-300 bg-slate-50 flex items-center justify-center overflow-hidden text-slate-400 text-xs"
      >
        <img v-if="previewUrl" :src="previewUrl" alt="preview" class="w-full h-full object-cover" />
        <span v-else>No image</span>
      </div>

      <div class="flex flex-col gap-2">
        <button
          type="button"
          class="px-3 py-1.5 rounded-md bg-koot-blue text-white text-sm font-medium disabled:opacity-60"
          :disabled="uploading"
          @click="trigger"
        >
          {{ uploading ? 'Uploading…' : modelValue ? 'Replace' : 'Upload image' }}
        </button>
        <button
          v-if="modelValue"
          type="button"
          class="text-sm text-slate-500 underline hover:text-slate-700"
          @click="clearImage"
        >
          Remove
        </button>
        <p v-if="errorMsg" class="text-xs text-koot-magenta" role="alert">{{ errorMsg }}</p>
        <p class="text-xs text-slate-400">JPG, PNG, GIF, or WEBP. Max 5 MB.</p>
      </div>
    </div>

    <input
      ref="fileInput"
      type="file"
      accept="image/jpeg,image/png,image/gif,image/webp"
      class="hidden"
      @change="onChange"
    />
  </div>
</template>
