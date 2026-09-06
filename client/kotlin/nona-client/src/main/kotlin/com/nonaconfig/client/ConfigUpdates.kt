package com.nonaconfig.client

import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow

/** One pending key set per collector; no background scope and no historical replay. */
internal class ConfigUpdates {
    private val lock = Any()
    private val subscribers = mutableSetOf<Subscriber>()

    val flow: Flow<Set<String>> = flow {
        val subscriber = Subscriber()
        synchronized(lock) { subscribers.add(subscriber) }
        try {
            for (signal in subscriber.signal) {
                val keys = subscriber.drain()
                if (keys.isNotEmpty()) emit(keys)
            }
        } finally {
            synchronized(lock) { subscribers.remove(subscriber) }
            subscriber.signal.cancel()
        }
    }

    fun publish(keys: Set<String>) {
        val current = synchronized(lock) { subscribers.toList() }
        current.forEach { it.add(keys) }
    }

    private class Subscriber {
        val signal = Channel<Unit>(Channel.CONFLATED)
        private val lock = Any()
        private var pending = linkedSetOf<String>()

        fun add(keys: Set<String>) {
            synchronized(lock) { pending.addAll(keys) }
            // Resuming a collector can execute user code; do it outside the lock.
            signal.trySend(Unit)
        }

        fun drain(): Set<String> = synchronized(lock) {
            pending.also { pending = linkedSetOf() }
        }
    }
}
