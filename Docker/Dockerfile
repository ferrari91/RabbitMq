# Base image
FROM rabbitmq:3-management

# Environment variables
ENV RABBITMQ_DEFAULT_USER=guest
ENV RABBITMQ_DEFAULT_PASS=guest

# Expose
EXPOSE 5672 15672

# Entry point
CMD ["rabbitmq-server"]
