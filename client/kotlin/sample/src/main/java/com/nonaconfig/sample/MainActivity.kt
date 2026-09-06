package com.nonaconfig.sample

import android.app.Activity
import android.os.Bundle
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import com.nonaconfig.client.NonaConfig
import java.util.concurrent.CompletableFuture

/** Pass frontendKey and optionally baseUrl as intent extras for local QA. */
class MainActivity : Activity() {
    private lateinit var config: NonaConfig
    private lateinit var status: TextView
    private val requests = mutableListOf<CompletableFuture<*>>()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val preferences = getSharedPreferences("connection", MODE_PRIVATE)
        intent.getStringExtra("frontendKey")?.let { preferences.edit().putString("key", it).apply() }
        intent.getStringExtra("baseUrl")?.let { preferences.edit().putString("url", it).apply() }
        val key = preferences.getString("key", "").orEmpty()
        val url = preferences.getString("url", "http://10.0.2.2:18686").orEmpty()
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(40, 64, 40, 40)
            setOnApplyWindowInsetsListener { view, insets ->
                view.setPadding(40, insets.systemWindowInsetTop + 32, 40, insets.systemWindowInsetBottom + 32)
                insets
            }
        }
        layout.addView(TextView(this).apply { setText(R.string.title); textSize = 26f })
        status = TextView(this).apply { textSize = 18f }
        layout.addView(status)
        setContentView(layout)
        if (key.isEmpty()) {
            status.setText(R.string.setup)
            return
        }
        config = JavaClient.create(this, url, key)
        config.setDefaults(mapOf("flag" to "default", "Limits:Retries" to 3))
        fun button(label: Int, action: () -> Unit) {
            layout.addView(Button(this).apply { setText(label); setOnClickListener { action() } })
        }
        button(R.string.fetch) { run(R.string.fetch, config.fetchAsync()) }
        button(R.string.activate) { render(getString(R.string.result, getString(R.string.activate), config.activate().toString())) }
        button(R.string.refresh) { run(R.string.refresh, config.fetchAndActivateAsync()) }
        button(R.string.reset) { run(R.string.reset, config.resetAsync()) }
        run(R.string.restored, config.initializeAsync())
    }

    private fun run(label: Int, future: CompletableFuture<*>) {
        requests += future
        future.whenComplete { result, error ->
            runOnUiThread {
                requests.remove(future)
                if (!isDestroyed) render(if (error == null) getString(R.string.result, getString(label), result?.toString() ?: getString(R.string.done)) else getString(R.string.failure, getString(label)))
            }
        }
    }

    private fun render(message: String) {
        status.text = getString(R.string.values, message, config.getString("flag"), config.getSource("flag").toString(), config.getLong("Limits:Retries"))
    }

    override fun onDestroy() {
        requests.toList().forEach { it.cancel(true) }
        super.onDestroy()
    }
}
