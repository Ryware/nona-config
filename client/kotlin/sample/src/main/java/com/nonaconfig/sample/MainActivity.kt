package com.nonaconfig.sample

import android.app.Activity
import android.os.Bundle
import android.os.Build
import android.view.WindowInsets
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import com.nonaconfig.client.NonaConfig
import java.util.concurrent.CompletableFuture

/** Pass frontendKey and optionally baseUrl as intent extras for local QA. */
class MainActivity : Activity() {
    private lateinit var config: NonaConfig
    private lateinit var status: TextView
    private var operationId = 0L
    private var closing = false
    private val requests = mutableListOf<CompletableFuture<*>>()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val preferences = getSharedPreferences("connection", MODE_PRIVATE)
        val key = intent.getStringExtra("frontendKey") ?: preferences.getString("key", "").orEmpty()
        val url = intent.getStringExtra("baseUrl")
            ?: preferences.getString("url", "http://10.0.2.2:18686").orEmpty()
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(40, 64, 40, 40)
            setOnApplyWindowInsetsListener { view, insets ->
                val (top, bottom) = verticalInsets(insets)
                view.setPadding(40, top + 32, 40, bottom + 32)
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
        config = try {
            JavaClient.create(this, url, key)
        } catch (_: IllegalArgumentException) {
            status.setText(R.string.invalid_connection)
            return
        } catch (_: java.net.URISyntaxException) {
            status.setText(R.string.invalid_connection)
            return
        }
        preferences.edit().putString("key", key).putString("url", url).apply()
        config.setDefaults(mapOf("flag" to "default", "Limits:Retries" to 3))
        fun button(label: Int, action: () -> Unit) {
            layout.addView(Button(this).apply { setText(label); setOnClickListener { action() } })
        }
        button(R.string.fetch) { run(R.string.fetch, config.fetchAsync()) }
        button(R.string.activate) { operationId++; render(getString(R.string.result, getString(R.string.activate), config.activate().toString())) }
        button(R.string.refresh) { run(R.string.refresh, config.fetchAndActivateAsync()) }
        button(R.string.reset) { run(R.string.reset, config.resetAsync()) }
        run(R.string.restored, config.initializeAsync())
    }

    private fun run(label: Int, future: CompletableFuture<*>) {
        val id = ++operationId
        requests += future
        future.whenComplete { result, error ->
            runOnUiThread {
                requests.remove(future)
                if (!closing && !isDestroyed && operationId == id) render(if (error == null) getString(R.string.result, getString(label), result?.toString() ?: getString(R.string.done)) else getString(R.string.failure, getString(label)))
            }
        }
    }

    private fun render(message: String) {
        status.text = getString(R.string.values, message, config.getString("flag"), config.getSource("flag").toString(), config.getLong("Limits:Retries"))
    }

    @Suppress("DEPRECATION") // The legacy accessors are required on API 24–29.
    private fun verticalInsets(insets: WindowInsets): Pair<Int, Int> =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            insets.getInsets(WindowInsets.Type.systemBars()).let { it.top to it.bottom }
        } else {
            insets.systemWindowInsetTop to insets.systemWindowInsetBottom
        }

    override fun onDestroy() {
        closing = true
        operationId++
        requests.toList().forEach { it.cancel(true) }
        super.onDestroy()
    }
}
